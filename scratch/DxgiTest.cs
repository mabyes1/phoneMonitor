using System;
using System.Diagnostics;
using System.Drawing;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace DxgiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting DXGI Desktop Duplication Diagnostic Test...");

            string deviceName = "\\\\.\\DISPLAY1";
            if (args.Length > 0)
            {
                deviceName = args[0];
            }

            Console.WriteLine($"Target Display: {deviceName}");

            try
            {
                using (var factory = new Factory1())
                {
                    Adapter1 selectedAdapter = null;
                    Output selectedOutput = null;

                    foreach (var adapter in factory.Adapters1)
                    {
                        foreach (var output in adapter.Outputs)
                        {
                            Console.WriteLine($"Found display: {output.Description.DeviceName} ({output.Description.DesktopBounds.Width}x{output.Description.DesktopBounds.Height})");
                            if (string.Equals(output.Description.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                            {
                                selectedAdapter = adapter;
                                selectedOutput = output;
                            }
                        }
                    }

                    if (selectedOutput == null)
                    {
                        if (factory.Adapters1.Length > 0 && factory.Adapters1[0].Outputs.Length > 0)
                        {
                            Console.WriteLine($"Display {deviceName} not found. Falling back to default display {factory.Adapters1[0].Outputs[0].Description.DeviceName}");
                            selectedAdapter = factory.Adapters1[0];
                            selectedOutput = factory.Adapters1[0].Outputs[0];
                        }
                        else
                        {
                            Console.WriteLine("Error: No displays/adapters found.");
                            return;
                        }
                    }

                    using (var d3dDevice = new Device(selectedAdapter, DeviceCreationFlags.None, SharpDX.Direct3D.FeatureLevel.Level_11_0))
                    using (var output1 = selectedOutput.QueryInterface<Output1>())
                    using (var duplication = output1.DuplicateOutput(d3dDevice))
                    {
                        Console.WriteLine("DXGI Desktop Duplication initialized successfully!");
                        Console.WriteLine("Running 5-second capture loop... Move your mouse or open a window to trigger screen updates.");

                        int successCount = 0;
                        int timeoutCount = 0;
                        int otherCount = 0;
                        var stopwatch = Stopwatch.StartNew();

                        while (stopwatch.ElapsedMilliseconds < 5000)
                        {
                            var result = duplication.TryAcquireNextFrame(10, out var frameInfo, out var resource);
                            if (result.Success)
                            {
                                successCount++;
                                resource.Dispose();
                                duplication.ReleaseFrame();
                            }
                            else if (result.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                            {
                                timeoutCount++;
                            }
                            else
                            {
                                otherCount++;
                            }
                            System.Threading.Thread.Sleep(16); // Simulate ~60fps polling
                        }

                        Console.WriteLine("\n--- Diagnostic Results ---");
                        Console.WriteLine($"Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
                        Console.WriteLine($"Successful frames captured: {successCount}");
                        Console.WriteLine($"Timeout frames (no change): {timeoutCount}");
                        Console.WriteLine($"Other failures: {otherCount}");

                        if (successCount == 0)
                        {
                            Console.WriteLine("\nWARNING: Captured 0 frames. DXGI Desktop Duplication is NOT receiving screen updates on this display.");
                        }
                        else
                        {
                            Console.WriteLine("\nSUCCESS: DXGI Desktop Duplication is working perfectly.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nDXGI Initialization Failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
