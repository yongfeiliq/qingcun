using System;
using System.Runtime.InteropServices;
static class MacBootstrap {
 static int Main(string[] args) {
  bool install=args.Length>0&&args[0].StartsWith("--mac-",StringComparison.Ordinal);
  try {
   if(install)Console.Error.WriteLine("[bootstrap] "+args[0]+" · "+RuntimeInformation.OSDescription+" · "+RuntimeInformation.ProcessArchitecture);
   // This branch must stay before Host initialization: diagnostics must not create data/jobs.
   if(args.Length==1 && args[0]=="--mac-runtime-check") {
    var probe=new { ok=true, stage="managed-runtime", framework=RuntimeInformation.FrameworkDescription, architecture=RuntimeInformation.ProcessArchitecture.ToString() };
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(probe));
    return 0;
   }
   return Host.Main(args);
  } catch(Exception e) {
   Console.Error.WriteLine("[bootstrap] 启动失败："+e);
   return 1;
  }
 }
}
