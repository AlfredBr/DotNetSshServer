#:sdk Aspire.AppHost.Sdk@13.1.3

#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddCSharpApp("sshserver-demo", @"..\SshServer.Demo\SshServer.Demo.csproj")
    .WithEnvironment("SSHSERVER_PORT", "2222")
    .WithEnvironment("SSHSERVER_ALLOWANONYMOUS", "false")
    .WithEnvironment("SSHSERVER_MAXCONNECTIONS", "100")
    .WithEnvironment("SSHSERVER_SESSIONTIMEOUTMINUTES", "2");

builder.Build().Run();
