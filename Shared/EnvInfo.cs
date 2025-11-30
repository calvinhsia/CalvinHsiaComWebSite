using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Client.Shared
{
    public class EnvInfo
    {
        public async Task<string> GetDataAsync()
        {
            await Task.Yield();
            var ticks = TimeSpan.FromMilliseconds(System.Environment.TickCount); // TickCount64  in client?
            var lastBootTime = DateTime.UtcNow.Subtract(ticks);
            //System.PlatformNotSupportedException: System.Diagnostics.Process is not supported on this platform.
            string CurrentProcess;
            string ProcessId;
            try
            {
                CurrentProcess = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                ProcessId = System.Diagnostics.Process.GetCurrentProcess()?.Id.ToString()!;
            }
            catch (Exception ex)
            {
                CurrentProcess = ex.Message;
                ProcessId = ex.Message;
            }
            // test calling an external api:
            string ExternalApiCall;
            try
            {
                var site = @"https://vscodefabrictestfunction.azurewebsites.net/api/envfunction";
                var url = new Uri(site);
                var httpc = new HttpClient();
                var res = await httpc.GetAsync(url);
                var content = await res.Content.ReadAsStringAsync();
                ExternalApiCall = content;
            }
            catch (Exception ex)
            {
                ExternalApiCall = ex.Message;
            }
            var myPixFileExists = false;
            try
            {
                myPixFileExists = System.IO.File.Exists(@"d:\home\MyPix.db");
            }
            catch (Exception)
            {
            }

            var sysObj = new
            {
                CurrentProcess,
                LocalDateTime = DateTime.Now.ToString(),
                OSVersion = $"{System.Environment.OSVersion}",
                ProcessId,
                ThreadIdMgd = Thread.CurrentThread.ManagedThreadId,
                CommandLine = $"{System.Environment.CommandLine}",
                System.Environment.CurrentDirectory,
                DotNetVersion = System.Environment.Version,
                System.Environment.Is64BitProcess,
                ComputerName = System.Environment.GetEnvironmentVariable("COMPUTERNAME"),
                System.Environment.UserName,
                UserDomain = System.Environment.UserDomainName,
                System.Environment.ProcessorCount,
                LastBootTime = lastBootTime,
                SystemUpTime = (DateTime.UtcNow - lastBootTime).ToString(),
                IntPtrSize = IntPtr.Size,
                myPixFileExists,
                ExternalApiCall,
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            var sysObjJson = JsonSerializer.Serialize(sysObj, options);

            return sysObjJson;
        }
    }
}
