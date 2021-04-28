/// MIT License, Copyright Burak Kara, burak@burak.io, https://en.wikipedia.org/wiki/MIT_License

using System.Collections.Generic;
using System.Threading;
using ApiGatewayService.Endpoints;
using BCloudServiceUtilities;
using BServiceUtilities;
using BWebServiceUtilities;
using ServiceUtilities;
using ServiceUtilities.All;

namespace ApiGatewayService
{
    class Program
    {
        static void Main()
        {
            System.Console.WriteLine("Initializing the service...");

//#if (Debug || DEBUG)
//            if (!ServicesDebugOnlyUtilities.CalledFromMain()) return;
//#endif

            // In case of a cloud component dependency or environment variable is added/removed;
            // Relative terraform script and microservice-dependency-map.cs must be updated as well.

            /*
            * Common initialization step
            */
            if (!BServiceInitializer.Initialize(out BServiceInitializer ServInit,
                new string[][]
                {
                    new string[] { "DEPLOYMENT_BRANCH_NAME" },
                    new string[] { "DEPLOYMENT_BUILD_NUMBER" },

                    new string[] { "AUTH_SERVICE_BASE_URL" },
                    new string[] { "CAD_FILE_SERVICE_BASE_URL" },
                    new string[] { "CUSTOM_PROCEDURES_SERVICE_BASE_URL" },
                    new string[] { "SCHEDULER_SERVICE_BASE_URL" }
                }))
                return;
            bool bInitSuccess = true;
            //bInitSuccess &= ServInit.WithTracingService();
            if (!bInitSuccess) return;

            Resources_DeploymentManager.Get().SetDeploymentBranchNameAndBuildNumber(ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BRANCH_NAME"], ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BUILD_NUMBER"]);

            /*
            * Web-http service initialization
            */
            var AuthServiceBaseUrl = ServInit.RequiredEnvironmentVariables["AUTH_SERVICE_BASE_URL"];
            var CadFileServiceBaseUrl = ServInit.RequiredEnvironmentVariables["CAD_FILE_SERVICE_BASE_URL"];
            var CustomProceduresServiceBaseUrl = ServInit.RequiredEnvironmentVariables["CUSTOM_PROCEDURES_SERVICE_BASE_URL"];
            var SchedulerServiceBaseUrl = ServInit.RequiredEnvironmentVariables["SCHEDULER_SERVICE_BASE_URL"];

            var RootPath = "/";
            if (ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BRANCH_NAME"] != "master" && ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BRANCH_NAME"] != "development")
            {
                RootPath = "/" + ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BRANCH_NAME"] + "/";
            }

            var WebServiceEndpoints = new List<BWebPrefixStructure>()
            {
                new BWebPrefixStructure(new string[] { RootPath }, () => new AzureApplicationGatewayRootRequest()/*Return OK for avoiding 502 Bad Gateway error from Microsoft-Azure-Application-Gateway health check*/),
                new BWebPrefixStructure(new string[] { RootPath + "auth/ping" }, () => new HandleRequest(AuthServiceBaseUrl, RootPath)/*Ping-pong; for avoiding scale-down-to-zero*/),
                new BWebPrefixStructure(new string[] { RootPath + "auth/internal/*" }, () => new HandleRequest(AuthServiceBaseUrl, RootPath)/*Internal services have secret based auth check, different than login mechanism*/),
                new BWebPrefixStructure(new string[] { RootPath + "auth/login*" }, () => new HandleRequest(AuthServiceBaseUrl, RootPath)/*For login requests*/),
                new BWebPrefixStructure(new string[] { RootPath + "auth/*" }, () => new HandleRequest(AuthServiceBaseUrl, RootPath).WithLoginRequirement(AuthServiceBaseUrl)/*Required from external*/),
                new BWebPrefixStructure(new string[] { RootPath + "3d/models/ping" }, () => new HandleRequest(CadFileServiceBaseUrl, RootPath)/*Ping-pong; for avoiding scale-down-to-zero*/),
                new BWebPrefixStructure(new string[] { RootPath + "3d/models/internal/*" }, () => new HandleRequest(CadFileServiceBaseUrl, RootPath)/*Internal services have secret based auth check, different than login mechanism*/),
                new BWebPrefixStructure(new string[] { RootPath + "3d/models*" }, () => new HandleRequest(CadFileServiceBaseUrl, RootPath).WithLoginRequirement(AuthServiceBaseUrl)),
                new BWebPrefixStructure(new string[] { RootPath + "scheduler/internal/*" }, () => new HandleRequest(SchedulerServiceBaseUrl, RootPath)/*Internal services have secret based auth check, different than login mechanism*/),
            };
            var BWebService = new BWebService(WebServiceEndpoints.ToArray(), ServInit.ServerPort/*, ServInit.TracingService*/);
            BWebService.Run((string Message) =>
            {
                ServInit.LoggingService.WriteLogs(BLoggingServiceMessageUtility.Single(EBLoggingServiceLogType.Info, Message), ServInit.ProgramID, "WebService");
            });

            /*
            * Make main thread sleep forever
            */
            Thread.Sleep(Timeout.Infinite);
        }
    }
}