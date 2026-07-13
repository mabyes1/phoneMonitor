using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhoneMonitor.Host.Dashboard;

namespace PhoneMonitor.Host.CustomSources
{
    public sealed class CustomSourceService
    {
        private static readonly Regex SourceKeyPattern = new Regex(
            "^[a-z0-9][a-z0-9-]{1,46}[a-z0-9]$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ItemKeyPattern = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly string DummyTokenHash = HashToken("pms_dummy_custom_source_token");

        private readonly CustomSourceStore store;
        private readonly CustomSourceOptions options;
        private readonly DashboardEventHub eventHub;
        private readonly ConcurrentDictionary<string, TokenBucket> rateBuckets =
            new ConcurrentDictionary<string, TokenBucket>(StringComparer.OrdinalIgnoreCase);

        public CustomSourceService(
            CustomSourceStore store,
            CustomSourceOptions options,
            DashboardEventHub eventHub)
        {
            this.store = store;
            this.options = options;
            this.options.Normalize();
            this.eventHub = eventHub;
        }

        public IReadOnlyList<CustomSourceManagementView> GetSources(DateTimeOffset now)
        {
            return store.GetSources()
                .Where(source => !IsSystemSource(source.SourceKey))
                .Select(source =>
                {
                    var itemCount = store.GetItemCount(source, now);
                    var view = CustomSourceStore.ToManagementView(source, now);
                    view.ItemCount = itemCount;
                    return view;
                })
                .ToList();
        }

        public CustomCardsResponse GetCardSnapshot(DateTimeOffset now)
        {
            return new CustomCardsResponse
            {
                GeneratedAt = CustomSourceDateTime.ToText(now),
                Cards = store.GetCardSnapshots(now)
            };
        }

        public CustomCardSettingsResponse GetCardSettings(string cardId)
        {
            var source = store.GetSourceByCardId(cardId);
            if (source == null) Problem(404, "card_not_found", "The custom card was not found.");
            return BuildCardSettings(source, store.GetCardSettings(source.Card.Id));
        }

        public CustomCardSettingsResponse UpdateCardSettings(
            string cardId,
            CustomCardSettingsUpdateRequest request,
            DateTimeOffset now)
        {
            if (request == null) Problem(400, "invalid_request", "A JSON request body is required.");
            var source = store.GetSourceByCardId(cardId);
            if (source == null) Problem(404, "card_not_found", "The custom card was not found.");
            var current = store.GetCardSettings(source.Card.Id);
            var maxItems = request.MaxItems ?? source.Card.MaxItems;
            var streamEnabled = request.StreamEnabled ?? current.StreamEnabled;
            var streamCharDelayMs = request.StreamCharDelayMs ?? current.StreamCharDelayMs;

            if (maxItems < CustomCardSettingsDefaults.MinVisibleItems || maxItems > CustomCardSettingsDefaults.MaxVisibleItems)
            {
                Problem(400, "invalid_card_settings", $"maxItems must be between {CustomCardSettingsDefaults.MinVisibleItems} and {CustomCardSettingsDefaults.MaxVisibleItems}.", "maxItems", "range");
            }
            if (streamCharDelayMs < CustomCardSettingsDefaults.MinStreamCharDelayMs || streamCharDelayMs > CustomCardSettingsDefaults.MaxStreamCharDelayMs)
            {
                Problem(400, "invalid_card_settings", $"streamCharDelayMs must be between {CustomCardSettingsDefaults.MinStreamCharDelayMs} and {CustomCardSettingsDefaults.MaxStreamCharDelayMs}.", "streamCharDelayMs", "range");
            }

            var updated = store.UpdateCardSettings(source.Card.Id, maxItems, streamEnabled, streamCharDelayMs, now);
            var settings = BuildCardSettings(updated, store.GetCardSettings(updated.Card.Id));
            eventHub.Publish("custom-card", new
            {
                cardId = updated.Card.Id,
                sourceKey = updated.SourceKey,
                revision = updated.Card.Revision,
                reason = "config"
            });
            return settings;
        }

        public CustomClearResult ClearCard(string cardId, DateTimeOffset now)
        {
            var result = store.ClearCard(cardId, now);
            if (result.Cleared)
            {
                eventHub.Publish("custom-card", new
                {
                    cardId = result.CardId,
                    sourceKey = result.SourceKey,
                    revision = result.Revision,
                    reason = "cleared"
                });
            }
            return result;
        }

        public CustomSourceRecord EnsureSystemSource(DateTimeOffset now)
        {
            var source = store.GetSource(CustomSourceKeys.WindowsNotifications);
            if (source != null)
            {
                if (!CustomSourceCardTypes.IsFeed(source.Card.Type))
                {
                    throw new CustomSourceStoreUnavailableException(
                        $"Reserved source '{CustomSourceKeys.WindowsNotifications}' has an incompatible card type.");
                }
                return source;
            }

            var systemSource = new CustomSourceRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceKey = CustomSourceKeys.WindowsNotifications,
                DisplayName = "Windows 通知",
                TokenHash = DummyTokenHash,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            systemSource.Card = new CustomCardRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceId = systemSource.Id,
                CardKey = "default",
                Type = CustomSourceCardTypes.MessageFeed,
                Title = "Windows 通知",
                Position = 0,
                StaleAfterSeconds = 0,
                DefaultTtlSeconds = 0,
                MaxItems = 30,
                Revision = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            return store.EnsureSystemSource(systemSource, systemSource.Card);
        }

        public CustomIngestResult IngestSystem(string sourceKey, JsonElement payload, DateTimeOffset now)
        {
            if (!IsSystemSource(sourceKey))
            {
                Problem(404, "source_not_found", "The system source was not found.");
            }

            var source = store.GetSource(CustomSourceKeys.WindowsNotifications);
            if (source == null) Problem(503, "custom_sources_unavailable", "The Windows notification source is unavailable.");
            var normalized = NormalizePayload(source, payload, now);
            var result = store.Ingest(source, normalized);
            eventHub.Publish("custom-card", new
            {
                cardId = result.CardId,
                sourceKey = result.SourceKey,
                revision = result.Revision,
                reason = "updated"
            });
            return result;
        }

        public CustomSourceCreateResponse Create(
            CustomSourceCreateRequest request,
            string endpointUrl,
            string localEndpointUrl,
            DateTimeOffset now)
        {
            if (request == null) Problem(400, "invalid_request", "A JSON request body is required.");
            var sourceKey = NormalizeSourceKey(request.SourceKey, true);
            EnsureNotSystemSource(sourceKey);
            var displayName = RequiredText(request.DisplayName, "displayName", 80);
            if (request.Card == null) Problem(400, "invalid_request", "card is required.");

            var type = NormalizeCardType(request.Card.Type);
            var title = string.IsNullOrWhiteSpace(request.Card.Title) ? displayName : RequiredText(request.Card.Title, "card.title", 80);
            var position = request.Card.Position ?? NextPosition();
            ValidatePosition(position);
            var staleAfter = request.Card.StaleAfterSeconds ?? 300;
            var defaultTtl = request.Card.DefaultTtlSeconds ?? 0;
            var maxItems = request.Card.MaxItems ?? 20;
            ValidateDurations(staleAfter, defaultTtl);
            ValidateMaxItems(maxItems);

            var token = GenerateToken();
            var source = new CustomSourceRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceKey = sourceKey,
                DisplayName = displayName,
                TokenHash = HashToken(token),
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            var card = new CustomCardRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceId = source.Id,
                CardKey = "default",
                Type = type,
                Title = title,
                Position = position,
                StaleAfterSeconds = staleAfter,
                DefaultTtlSeconds = defaultTtl,
                MaxItems = maxItems,
                Revision = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            source.Card = card;

            var response = store.CreateSource(source, card, options.MaxSources, endpointUrl, localEndpointUrl, token);
            response.Source = CustomSourceStore.ToManagementView(source, now);
            response.Source.Card.Revision = 0;
            return response;
        }

        public CustomSourceManagementView Update(string sourceKey, CustomSourceUpdateRequest request, DateTimeOffset now)
        {
            var normalizedKey = NormalizeSourceKey(sourceKey, false);
            EnsureNotSystemSource(normalizedKey);
            var source = store.GetSource(normalizedKey);
            if (source == null) Problem(404, "source_not_found", "The custom source was not found.");
            if (request == null) Problem(400, "invalid_request", "A JSON request body is required.");

            RejectImmutableUpdate(request.SourceKey, "sourceKey");
            RejectImmutableUpdate(request.CardId, "cardId");
            RejectImmutableUpdate(request.CardKey, "cardKey");
            RejectImmutableUpdate(request.Type, "type");
            if (request.Card != null)
            {
                RejectImmutableUpdate(request.Card.Type, "card.type");
                RejectImmutableUpdate(request.Card.CardId, "card.cardId");
                RejectImmutableUpdate(request.Card.CardKey, "card.cardKey");
            }

            var displayName = request.DisplayName == null
                ? source.DisplayName
                : RequiredText(request.DisplayName, "displayName", 80);
            var enabled = request.Enabled ?? source.Enabled;
            var card = source.Card;
            var title = card.Title;
            var position = card.Position;
            var staleAfter = card.StaleAfterSeconds;
            var defaultTtl = card.DefaultTtlSeconds;
            var maxItems = card.MaxItems;

            if (request.Card != null)
            {
                if (request.Card.Title != null) title = RequiredText(request.Card.Title, "card.title", 80);
                if (request.Card.Position.HasValue) position = request.Card.Position.Value;
                if (request.Card.StaleAfterSeconds.HasValue) staleAfter = request.Card.StaleAfterSeconds.Value;
                if (request.Card.DefaultTtlSeconds.HasValue) defaultTtl = request.Card.DefaultTtlSeconds.Value;
                if (request.Card.MaxItems.HasValue) maxItems = request.Card.MaxItems.Value;
            }

            ValidatePosition(position);
            ValidateDurations(staleAfter, defaultTtl);
            ValidateMaxItems(maxItems);
            var cardChanged = !string.Equals(card.Title, title, StringComparison.Ordinal) ||
                card.Position != position ||
                card.StaleAfterSeconds != staleAfter ||
                card.DefaultTtlSeconds != defaultTtl ||
                card.MaxItems != maxItems;
            var visibilityChanged = source.Enabled != enabled;

            var updated = store.UpdateSource(
                normalizedKey,
                displayName,
                enabled,
                title,
                position,
                staleAfter,
                defaultTtl,
                maxItems,
                cardChanged,
                now);
            var view = CustomSourceStore.ToManagementView(updated, now);
            view.ItemCount = store.GetItemCount(updated, now);
            if (cardChanged || visibilityChanged)
            {
                eventHub.Publish("custom-card", new
                {
                    cardId = updated.Card.Id,
                    sourceKey = updated.SourceKey,
                    revision = updated.Card.Revision,
                    reason = "config"
                });
            }
            return view;
        }

        public CustomSourceCreateResponse RotateToken(
            string sourceKey,
            string endpointUrl,
            string localEndpointUrl,
            DateTimeOffset now)
        {
            var normalizedKey = NormalizeSourceKey(sourceKey, false);
            EnsureNotSystemSource(normalizedKey);
            var source = store.GetSource(normalizedKey);
            if (source == null) Problem(404, "source_not_found", "The custom source was not found.");
            var token = GenerateToken();
            store.RotateToken(normalizedKey, HashToken(token), now);
            var response = new CustomSourceCreateResponse
            {
                Source = CustomSourceStore.ToManagementView(source, now),
                Ingest = new CustomSourceIngestInfo
                {
                    EndpointPath = $"/api/custom-sources/{normalizedKey}/events",
                    EndpointUrl = endpointUrl,
                    LocalEndpointUrl = localEndpointUrl,
                    Token = token
                }
            };
            response.Source.ItemCount = store.GetItemCount(source, now);
            return response;
        }

        public CustomDeleteResult DeleteSource(string sourceKey)
        {
            var normalizedKey = NormalizeSourceKey(sourceKey, false);
            EnsureNotSystemSource(normalizedKey);
            var change = store.DeleteSource(normalizedKey);
            rateBuckets.TryRemove(normalizedKey, out _);
            eventHub.Publish("custom-card", new
            {
                cardId = change.CardId,
                sourceKey = change.SourceKey,
                revision = change.Revision,
                reason = "deleted"
            });
            return new CustomDeleteResult
            {
                Deleted = true,
                SourceKey = change.SourceKey,
                CardId = change.CardId,
                Revision = change.Revision
            };
        }

        public CustomIngestResult Ingest(string sourceKey, string token, JsonElement payload, DateTimeOffset now)
        {
            var source = AuthenticateForWrite(sourceKey, token);
            EnsureRateLimit(source, now);

            var normalized = NormalizePayload(source, payload, now);
            var result = store.Ingest(source, normalized);
            eventHub.Publish("custom-card", new
            {
                cardId = result.CardId,
                sourceKey = result.SourceKey,
                revision = result.Revision,
                reason = "updated"
            });
            return result;
        }

        public CustomDeleteResult DeleteItem(string sourceKey, string token, string itemKey, DateTimeOffset now)
        {
            var source = AuthenticateForWrite(sourceKey, token);
            EnsureRateLimit(source, now);
            if (!CustomSourceCardTypes.IsFeed(source.Card.Type))
            {
                Problem(409, "card_type_mismatch", "Item deletion only applies to message-feed cards.");
            }
            if (!ItemKeyPattern.IsMatch(itemKey ?? string.Empty))
            {
                Problem(400, "invalid_item_key", "The item id is not valid.");
            }
            var result = store.DeleteItem(source.SourceKey, itemKey, now);
            eventHub.Publish("custom-card", new
            {
                cardId = result.CardId,
                sourceKey = result.SourceKey,
                revision = result.Revision,
                reason = "deleted"
            });
            return result;
        }

        public CustomClearResult ClearState(string sourceKey, string token, DateTimeOffset now)
        {
            var source = AuthenticateForWrite(sourceKey, token);
            EnsureRateLimit(source, now);
            if (CustomSourceCardTypes.IsFeed(source.Card.Type))
            {
                Problem(409, "card_type_mismatch", "State deletion does not apply to message-feed cards.");
            }
            var result = store.ClearState(source.SourceKey, now);
            if (result.Cleared)
            {
                eventHub.Publish("custom-card", new
                {
                    cardId = result.CardId,
                    sourceKey = result.SourceKey,
                    revision = result.Revision,
                    reason = "cleared"
                });
            }
            return result;
        }

        public void CleanupExpired(DateTimeOffset now)
        {
            foreach (var change in store.CleanupExpired(now))
            {
                eventHub.Publish("custom-card", new
                {
                    cardId = change.CardId,
                    sourceKey = change.SourceKey,
                    revision = change.Revision,
                    reason = change.Reason
                });
            }
        }

        private CustomSourceRecord AuthenticateForWrite(string sourceKey, string token)
        {
            var normalizedKey = NormalizeSourceKeyForLookup(sourceKey);
            var source = normalizedKey == null ? null : store.GetSource(normalizedKey);
            var suppliedHash = HashToken(token ?? string.Empty);
            var valid = FixedTimeEquals(suppliedHash, source?.TokenHash ?? DummyTokenHash);
            if (source == null || !valid)
            {
                throw new CustomSourceProblemException(401, "invalid_source_token", "The source token is invalid.");
            }
            if (!source.Enabled)
            {
                throw new CustomSourceProblemException(403, "source_disabled", "The custom source is disabled.");
            }
            return source;
        }

        private static bool IsSystemSource(string sourceKey)
        {
            return string.Equals(sourceKey, CustomSourceKeys.WindowsNotifications, StringComparison.OrdinalIgnoreCase);
        }

        private static CustomCardSettingsResponse BuildCardSettings(
            CustomSourceRecord source,
            CustomCardSettingsRecord settings)
        {
            return new CustomCardSettingsResponse
            {
                CardId = source.Card.Id,
                SourceKey = source.SourceKey,
                Title = source.Card.Title,
                Type = source.Card.Type,
                MaxItems = source.Card.MaxItems,
                StreamEnabled = settings.StreamEnabled,
                StreamCharDelayMs = settings.StreamCharDelayMs,
                UpdatedAt = CustomSourceDateTime.ToText(settings.UpdatedAt)
            };
        }

        private static void EnsureNotSystemSource(string sourceKey)
        {
            if (IsSystemSource(sourceKey))
            {
                Problem(403, "system_source", "The Windows notification source is managed by PhoneMonitor.");
            }
        }

        private CustomNormalizedPayload NormalizePayload(CustomSourceRecord source, JsonElement root, DateTimeOffset receivedAt)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                Problem(400, "invalid_payload", "The payload must be a JSON object.");
            }

            var severity = ReadOptionalString(root, "severity")?.Trim().ToLowerInvariant() ?? "info";
            if (!new[] { "info", "success", "warning", "error" }.Contains(severity, StringComparer.Ordinal))
            {
                Problem(400, "invalid_payload", "severity must be info, success, warning, or error.", "severity", "invalid");
            }

            var occurredAt = ReadTimestamp(root, "timestamp", receivedAt);
            var ttl = ReadTtl(root, source.Card.DefaultTtlSeconds);
            var expiresAt = ttl > 0 ? receivedAt.AddSeconds(ttl) : (DateTimeOffset?)null;

            switch (source.Card.Type)
            {
                case CustomSourceCardTypes.MessageFeed:
                    return NormalizeMessage(root, severity, occurredAt, receivedAt, expiresAt);
                case CustomSourceCardTypes.Status:
                    return NormalizeStatus(root, severity, occurredAt, receivedAt, expiresAt);
                case CustomSourceCardTypes.Metric:
                    return NormalizeMetric(root, severity, occurredAt, receivedAt, expiresAt);
                case CustomSourceCardTypes.KeyValue:
                    return NormalizeKeyValue(root, occurredAt, receivedAt, expiresAt);
                default:
                    Problem(503, "custom_sources_unavailable", "The custom source card type is not supported.");
                    return null;
            }
        }

        private static CustomNormalizedPayload NormalizeMessage(
            JsonElement root,
            string severity,
            DateTimeOffset occurredAt,
            DateTimeOffset receivedAt,
            DateTimeOffset? expiresAt)
        {
            var id = RequiredText(ReadOptionalString(root, "id"), "id", 128);
            if (!ItemKeyPattern.IsMatch(id)) Problem(400, "invalid_payload", "id contains unsupported characters.", "id", "invalid");
            var from = OptionalText(ReadOptionalString(root, "from"), "from", 80);
            var text = RequiredText(ReadOptionalString(root, "text"), "text", 2000);
            ValidateLines(text, "text");
            var content = new CustomMessageItem
            {
                Id = id,
                From = from,
                Text = text,
                Severity = severity,
                OccurredAt = CustomSourceDateTime.ToText(occurredAt),
                ReceivedAt = CustomSourceDateTime.ToText(receivedAt),
                ExpiresAt = CustomSourceDateTime.ToText(expiresAt)
            };
            return new CustomNormalizedPayload
            {
                ItemKey = id,
                ContentJson = Serialize(content),
                OccurredAt = occurredAt,
                ReceivedAt = receivedAt,
                ExpiresAt = expiresAt
            };
        }

        private static CustomNormalizedPayload NormalizeStatus(
            JsonElement root,
            string severity,
            DateTimeOffset occurredAt,
            DateTimeOffset receivedAt,
            DateTimeOffset? expiresAt)
        {
            var status = RequiredText(ReadOptionalString(root, "status"), "status", 120);
            var detail = OptionalText(ReadOptionalString(root, "detail"), "detail", 500);
            if (detail != null) ValidateLines(detail, "detail");
            var content = new CustomStatusContent
            {
                Status = status,
                Detail = detail,
                Severity = severity,
                OccurredAt = CustomSourceDateTime.ToText(occurredAt),
                ReceivedAt = CustomSourceDateTime.ToText(receivedAt),
                ExpiresAt = CustomSourceDateTime.ToText(expiresAt)
            };
            return new CustomNormalizedPayload
            {
                ItemKey = "current",
                ContentJson = Serialize(content),
                OccurredAt = occurredAt,
                ReceivedAt = receivedAt,
                ExpiresAt = expiresAt
            };
        }

        private static CustomNormalizedPayload NormalizeMetric(
            JsonElement root,
            string severity,
            DateTimeOffset occurredAt,
            DateTimeOffset receivedAt,
            DateTimeOffset? expiresAt)
        {
            var value = RequiredNumber(root, "value");
            var unit = OptionalText(ReadOptionalString(root, "unit"), "unit", 16);
            var detail = OptionalText(ReadOptionalString(root, "detail"), "detail", 500);
            if (detail != null) ValidateLines(detail, "detail");
            var progress = ReadOptionalNumber(root, "progress");
            if (progress.HasValue && (progress.Value < 0 || progress.Value > 100))
            {
                Problem(400, "invalid_payload", "progress must be between 0 and 100.", "progress", "range");
            }
            var content = new CustomMetricContent
            {
                Value = value,
                Unit = unit,
                Detail = detail,
                Progress = progress,
                Severity = severity,
                OccurredAt = CustomSourceDateTime.ToText(occurredAt),
                ReceivedAt = CustomSourceDateTime.ToText(receivedAt),
                ExpiresAt = CustomSourceDateTime.ToText(expiresAt)
            };
            return new CustomNormalizedPayload
            {
                ItemKey = "current",
                ContentJson = Serialize(content),
                OccurredAt = occurredAt,
                ReceivedAt = receivedAt,
                ExpiresAt = expiresAt
            };
        }

        private static CustomNormalizedPayload NormalizeKeyValue(
            JsonElement root,
            DateTimeOffset occurredAt,
            DateTimeOffset receivedAt,
            DateTimeOffset? expiresAt)
        {
            var itemsElement = ReadProperty(root, "items");
            if (!itemsElement.HasValue || itemsElement.Value.ValueKind != JsonValueKind.Array)
            {
                Problem(400, "invalid_payload", "items must be an array.", "items", "required");
            }

            var items = new List<CustomKeyValueItem>();
            foreach (var item in itemsElement.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    Problem(400, "invalid_payload", "Each item must be an object.", "items", "invalid");
                }
                var label = RequiredText(ReadOptionalString(item, "label"), "items.label", 50);
                var value = RequiredText(ReadOptionalString(item, "value"), "items.value", 200);
                items.Add(new CustomKeyValueItem { Label = label, Value = value });
            }
            if (items.Count < 1 || items.Count > 12)
            {
                Problem(400, "invalid_payload", "items must contain between 1 and 12 entries.", "items", "range");
            }

            var content = new CustomKeyValueContent
            {
                Items = items,
                OccurredAt = CustomSourceDateTime.ToText(occurredAt),
                ReceivedAt = CustomSourceDateTime.ToText(receivedAt),
                ExpiresAt = CustomSourceDateTime.ToText(expiresAt)
            };
            return new CustomNormalizedPayload
            {
                ItemKey = "current",
                ContentJson = Serialize(content),
                OccurredAt = occurredAt,
                ReceivedAt = receivedAt,
                ExpiresAt = expiresAt
            };
        }

        private int NextPosition()
        {
            var sources = store.GetSources();
            return sources.Count == 0 ? 100 : sources.Max(source => source.Card.Position) + 100;
        }

        private bool TryConsumeRate(string sourceKey, DateTimeOffset now, out int retryAfterSeconds)
        {
            var bucket = rateBuckets.GetOrAdd(
                sourceKey,
                _ => new TokenBucket(options.RequestsPerSecond, options.Burst, now));
            return bucket.TryConsume(now, out retryAfterSeconds);
        }

        private void EnsureRateLimit(CustomSourceRecord source, DateTimeOffset now)
        {
            if (TryConsumeRate(source.SourceKey, now, out var retryAfter)) return;
            throw new CustomSourceProblemException(
                429,
                "rate_limited",
                "The custom source is sending too quickly.",
                new Dictionary<string, string>
                {
                    ["retryAfterSeconds"] = retryAfter.ToString(CultureInfo.InvariantCulture)
                });
        }

        private static string NormalizeSourceKey(string value, bool validate)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (validate && !SourceKeyPattern.IsMatch(normalized))
            {
                Problem(400, "invalid_source_key", "sourceKey must use lowercase letters, numbers, and hyphens.", "sourceKey", "invalid");
            }
            return normalized;
        }

        private static string NormalizeSourceKeyForLookup(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return SourceKeyPattern.IsMatch(normalized) ? normalized : null;
        }

        private static string NormalizeCardType(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (!CustomSourceCardTypes.IsSupported(normalized))
            {
                Problem(400, "invalid_payload", "card.type is not supported.", "card.type", "invalid");
            }
            return normalized;
        }

        private static string RequiredText(string value, string field, int maxLength)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > maxLength)
            {
                Problem(400, "invalid_payload", $"{field} is required and must be at most {maxLength} characters.", field, "length");
            }
            return normalized;
        }

        private static string OptionalText(string value, string field, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim();
            if (normalized.Length > maxLength)
            {
                Problem(400, "invalid_payload", $"{field} must be at most {maxLength} characters.", field, "length");
            }
            return normalized;
        }

        private static void ValidateLines(string value, string field)
        {
            if (value.Count(character => character == '\n') + 1 > 8)
            {
                Problem(400, "invalid_payload", $"{field} must contain at most 8 lines.", field, "lines");
            }
        }

        private static void ValidatePosition(int position)
        {
            if (position < 0 || position > 10000) Problem(400, "invalid_payload", "position must be between 0 and 10000.", "card.position", "range");
        }

        private static void ValidateDurations(int staleAfter, int defaultTtl)
        {
            if (!IsAllowedDuration(staleAfter) || !IsAllowedDuration(defaultTtl))
            {
                Problem(400, "invalid_payload", "durations must be 0 or between 30 and 604800 seconds.", "card", "duration");
            }
        }

        private static bool IsAllowedDuration(int value) => value == 0 || value >= 30 && value <= 604800;

        private static void ValidateMaxItems(int maxItems)
        {
            if (maxItems < 1 || maxItems > 50) Problem(400, "invalid_payload", "maxItems must be between 1 and 50.", "card.maxItems", "range");
        }

        private static DateTimeOffset ReadTimestamp(JsonElement root, string name, DateTimeOffset fallback)
        {
            var raw = ReadOptionalString(root, name);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var value = raw.Trim();
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) || !HasExplicitOffset(value))
            {
                Problem(400, "invalid_payload", $"{name} must be an ISO 8601 timestamp with timezone.", name, "timestamp");
            }
            return parsed.ToUniversalTime();
        }

        private static bool HasExplicitOffset(string value)
        {
            var separator = value.IndexOf('T');
            if (separator < 0) separator = value.IndexOf('t');
            if (separator < 0) return false;
            var time = value.Substring(separator + 1);
            return time.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(time, "[+-][0-9]{2}:[0-9]{2}$", RegexOptions.CultureInvariant);
        }

        private static int ReadTtl(JsonElement root, int defaultTtl)
        {
            var property = ReadProperty(root, "ttlSeconds");
            if (!property.HasValue) return defaultTtl;
            var ttl = 0;
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out ttl) || ttl < 30 || ttl > 604800)
            {
                Problem(400, "invalid_payload", "ttlSeconds must be between 30 and 604800.", "ttlSeconds", "range");
            }
            return ttl;
        }

        private static double RequiredNumber(JsonElement root, string name)
        {
            var property = ReadProperty(root, name);
            var value = 0d;
            if (!property.HasValue || property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                Problem(400, "invalid_payload", $"{name} must be a finite JSON number.", name, "number");
            }
            return value;
        }

        private static double? ReadOptionalNumber(JsonElement root, string name)
        {
            var property = ReadProperty(root, name);
            if (!property.HasValue) return null;
            var value = 0d;
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                Problem(400, "invalid_payload", $"{name} must be a finite JSON number.", name, "number");
            }
            return value;
        }

        private static string ReadOptionalString(JsonElement root, string name)
        {
            var property = ReadProperty(root, name);
            if (!property.HasValue || property.Value.ValueKind == JsonValueKind.Null) return null;
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                Problem(400, "invalid_payload", $"{name} must be a string.", name, "string");
            }
            return property.Value.GetString();
        }

        private static JsonElement? ReadProperty(JsonElement root, string name)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
            }
            return null;
        }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return "pms_" + Convert.ToBase64String(bytes)
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');
        }

        private static string HashToken(string value)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
            return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static string Serialize(object value) => JsonSerializer.Serialize(value, CustomSourceJson.Options);

        private static void RejectImmutableUpdate(string value, string field)
        {
            if (!string.IsNullOrWhiteSpace(value)) Problem(400, "immutable_field", $"{field} cannot be changed.", field, "immutable");
        }

        private static void Problem(int statusCode, string code, string message, string field = null, string fieldValue = null)
        {
            throw new CustomSourceProblemException(
                statusCode,
                code,
                message,
                field == null ? null : new Dictionary<string, string> { [field] = fieldValue ?? "invalid" });
        }

        private sealed class TokenBucket
        {
            private readonly double refillPerSecond;
            private readonly double capacity;
            private readonly object sync = new object();
            private double tokens;
            private DateTimeOffset last;

            public TokenBucket(double refillPerSecond, double capacity, DateTimeOffset now)
            {
                this.refillPerSecond = refillPerSecond;
                this.capacity = capacity;
                tokens = capacity;
                last = now;
            }

            public bool TryConsume(DateTimeOffset now, out int retryAfterSeconds)
            {
                lock (sync)
                {
                    var elapsed = Math.Max(0, (now - last).TotalSeconds);
                    tokens = Math.Min(capacity, tokens + elapsed * refillPerSecond);
                    last = now;
                    if (tokens >= 1)
                    {
                        tokens -= 1;
                        retryAfterSeconds = 0;
                        return true;
                    }

                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((1 - tokens) / refillPerSecond));
                    return false;
                }
            }
        }
    }
}
