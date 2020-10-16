/// MIT License, Copyright Burak Kara, burak@burak.io, https://en.wikipedia.org/wiki/MIT_License

using System;
using System.Net;
using BWebServiceUtilities;
using BWebServiceUtilities_GC;
using ServiceUtilities.All;

namespace ApiPassThroughService.Endpoints
{
    internal class PassThroughRequest : BppWebServiceBase
    {
        private readonly string ApiGatewayServiceEndpoint;

        public PassThroughRequest(string _ApiGatewayServiceEndpoint)
        {
            ApiGatewayServiceEndpoint = _ApiGatewayServiceEndpoint.TrimEnd('/');
        }

        protected override BWebServiceResponse OnRequestPP(HttpListenerContext _Context, Action<string> _ErrorMessageAction = null)
        {
            if (MaintenanceChecker.Get().IsMaintenanceModeOn()) return BWebResponse.ServiceUnavailable("The system is in maintenance.");

            var Response = BWebUtilities_GC_CloudRun.RequestRedirection(
                _Context,
                ApiGatewayServiceEndpoint + "/" + _Context.Request.RawUrl.TrimStart('/'),
                _ErrorMessageAction,
                false,
                false);
 
            return MaintenanceChecker.Get().IsMaintenanceModeOn() ? BWebResponse.ServiceUnavailable("The system is in maintenance.") : Response;            
        }
    }
}