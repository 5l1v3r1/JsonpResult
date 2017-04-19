# JsonpResult
JsonpResult for .net mvc core
this is jsonpResult for .net mvc Core

howTo:

in "startup.cs" file

public void ConfigureServices(IServiceCollection services)
{

	......
  
        services.AddSingleton<JsonpResultExecutor>();
        
        ......
        
}

in you Controller

add

using SmartMap.NetPlatform.Core.Common;


return jsonp data like

this.Jsonp(you data);
