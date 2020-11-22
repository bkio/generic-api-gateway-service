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

#if (Debug || DEBUG)
            if (!ServicesDebugOnlyUtilities.CalledFromMain()) return;
#endif

            // In case of a cloud component dependency or environment variable is added/removed;
            // Relative terraform script and microservice-dependency-map.cs must be updated as well.

            /*
            * Common initialization step
            */
            if (!BServiceInitializer.Initialize(out BServiceInitializer ServInit,
                new string[][]
                {
                    new string[] { "GOOGLE_CLOUD_PROJECT_ID" },
                    new string[] { "GOOGLE_APPLICATION_CREDENTIALS", "GOOGLE_PLAIN_CREDENTIALS" },

                    new string[] { "DEPLOYMENT_BRANCH_NAME" },
                    new string[] { "DEPLOYMENT_BUILD_NUMBER" },

                    new string[] { "AUTH_SERVICE_BASE_URL" },
                    new string[] { "CAD_FILE_SERVICE_BASE_URL" },
                    new string[] { "CUSTOM_PROCEDURES_SERVICE_BASE_URL" },
                    new string[] { "SCHEDULER_SERVICE_BASE_URL" }
                }))
                return;
            bool bInitSuccess = true;
            bInitSuccess &= ServInit.WithTracingService();
            if (!bInitSuccess) return;

            Resources_DeploymentManager.Get().SetDeploymentBranchNameAndBuildNumber(ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BRANCH_NAME"], ServInit.RequiredEnvironmentVariables["DEPLOYMENT_BUILD_NUMBER"]);

            /*
            * Web-http service initialization
            */
            var AuthServiceBaseUrl = ServInit.RequiredEnvironmentVariables["AUTH_SERVICE_BASE_URL"];
            var CadFileServiceBaseUrl = ServInit.RequiredEnvironmentVariables["CAD_FILE_SERVICE_BASE_URL"];
            var CustomProceduresServiceBaseUrl = ServInit.RequiredEnvironmentVariables["CUSTOM_PROCEDURES_SERVICE_BASE_URL"];
            var SchedulerServiceBaseUrl = ServInit.RequiredEnvironmentVariables["SCHEDULER_SERVICE_BASE_URL"];

            var WebServiceEndpoints = new List<BWebPrefixStructure>()
            {
                new BWebPrefixStructure(new string[] { "/auth/ping" }, () => new HandleRequest(AuthServiceBaseUrl)/*Ping-pong; for avoiding scale-down-to-zero*/),
                new BWebPrefixStructure(new string[] { "/auth/internal/*" }, () => new HandleRequest(AuthServiceBaseUrl)/*Internal services have secret based auth check, different than login mechanism*/),
                new BWebPrefixStructure(new string[] { "/auth/login*" }, () => new HandleRequest(AuthServiceBaseUrl)/*For login requests*/),
                new BWebPrefixStructure(new string[] { "/auth/*" }, () => new HandleRequest(AuthServiceBaseUrl).WithLoginRequirement(AuthServiceBaseUrl)/*Required from external*/),
                new BWebPrefixStructure(new string[] { "/3d/models/ping" }, () => new HandleRequest(CadFileServiceBaseUrl)/*Ping-pong; for avoiding scale-down-to-zero*/),
                new BWebPrefixStructure(new string[] { "/3d/models/internal/*" }, () => new HandleRequest(CadFileServiceBaseUrl)/*Internal services have secret based auth check, different than login mechanism*/),
                new BWebPrefixStructure(new string[] { "/3d/models/*" }, () => new HandleRequest(CadFileServiceBaseUrl).WithLoginRequirement(AuthServiceBaseUrl)),
                new BWebPrefixStructure(new string[] { "/custom_procedures/ping" }, () => new HandleRequest(CustomProceduresServiceBaseUrl)/*Ping-pong; for avoiding scale-down-to-zero*/),
                new BWebPrefixStructure(new string[] { "/custom_procedures/internal/*" }, () => new HandleRequest(CustomProceduresServiceBaseUrl)/*Internal services have secret based auth check, different than login mechanism*/),
                new BWebPrefixStructure(new string[] { "/custom_procedures/*" }, () => new HandleRequest(CustomProceduresServiceBaseUrl).WithLoginRequirement(AuthServiceBaseUrl)),
                new BWebPrefixStructure(new string[] { "/scheduler/internal/*" }, () => new HandleRequest(SchedulerServiceBaseUrl)/*Internal services have secret based auth check, different than login mechanism*/),
            };
            var BWebService = new BWebService(WebServiceEndpoints.ToArray(), ServInit.ServerPort, ServInit.TracingService);
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