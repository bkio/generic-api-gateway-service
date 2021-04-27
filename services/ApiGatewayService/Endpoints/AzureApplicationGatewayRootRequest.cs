using BWebServiceUtilities;
using ServiceUtilities.All;
using System;
using System.Net;

namespace ApiGatewayService.Endpoints
{
    internal class AzureApplicationGatewayRootRequest : BppWebServiceBase
    {
        public AzureApplicationGatewayRootRequest()
        {
        }

        protected override BWebServiceResponse OnRequestPP(HttpListenerContext _Context, Action<string> _ErrorMessageAction = null)
        {
            return BWebResponse.StatusOK("OK");
        }
    }
}
