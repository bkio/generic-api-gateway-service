/// MIT License, Copyright Burak Kara, burak@burak.io, https://en.wikipedia.org/wiki/MIT_License

using System;
using System.Collections.Generic;
using System.Threading;
using ApiPassThroughService.Endpoints;
using BCommonUtilities;
using BWebServiceUtilities;
using ServiceUtilities;
using ServiceUtilities.All;

namespace ApiPassThroughService
{
    //Custom domains are not supported to perform synchronous calls yet. Therefore we need an api passthrough service
    //That is a bridge between client and api gateway.
    //https://stackoverflow.com/questions/58683365/google-cloud-run-authentication-service-to-service
    class Program
    {
        static int Main()
        {
            Console.WriteLine("Initializing the service...");

#if (Debug || DEBUG)
            if (!ServicesDebugOnlyUtilities.CalledFromMain()) return 1;
#endif

            // In case of a cloud component dependency or environment variable is added/removed;
            // Relative terraform script and microservice-dependency-map.cs must be updated as well.

            if (!BUtility.GetEnvironmentVariables(out Dictionary<string, string> EnvironmentVariables,
                new string[][]
                {
                    new string[] { "PORT" },
                    new string[] { "PROGRAM_ID" },

                    new string[] { "API_GATEWAY_SERVICE_BASE_URL" },

                    new string[] { "STATIC_STATE_BUCKET" }, //Currently set to ignore on local debugging sessions; since it is only used for maintenance check

                    new string[] { "DEPLOYMENT_BRANCH_NAME" },
                    new string[] { "DEPLOYMENT_BUILD_NUMBER" }
                },
                Console.WriteLine)) return 1;

            Resources_DeploymentManager.Get().SetDeploymentBranchNameAndBuildNumber(EnvironmentVariables["DEPLOYMENT_BRANCH_NAME"], EnvironmentVariables["DEPLOYMENT_BUILD_NUMBER"]);

            if (!int.TryParse(EnvironmentVariables["PORT"], out int ServerPort))
            {
                Console.WriteLine("Invalid PORT environment variable; it must be an integer.");
                return 1;
            }

            string ApiGatewayServiceEndpoint = EnvironmentVariables["API_GATEWAY_SERVICE_BASE_URL"];

            string MaintenanceModeCheckUrl = "https://storage.googleapis.com/" + EnvironmentVariables["STATIC_STATE_BUCKET"] + "/maintenance_mode_" + Resources_DeploymentManager.Get().GetDeploymentBranchName();

            var DeploymentLoweredBranchName = Resources_DeploymentManager.Get().GetDeploymentBranchNameEscapedLoweredWithDash();
            if (DeploymentLoweredBranchName == "master" || DeploymentLoweredBranchName == "development")
            {
                //If the branch is feature, bugfix, hotfix etc. do not check for maintenance.
                MaintenanceChecker.Get().Start(MaintenanceModeCheckUrl, Console.WriteLine);
            }

            /*
            * Web-http service initialization
            */
            var WebServiceEndpoints = new List<BWebPrefixStructure>()
            {
                new BWebPrefixStructure(new string[] { "*" }, () => new PassThroughRequest(ApiGatewayServiceEndpoint))
            };
            var BWebService = new BWebService(WebServiceEndpoints.ToArray(), ServerPort);
            BWebService.Run((string Message) =>
            {
                Console.WriteLine(Message);
            });

            /*
            * Make main thread sleep forever
            */
            Thread.Sleep(Timeout.Infinite);

            return 0;
        }
    }
}