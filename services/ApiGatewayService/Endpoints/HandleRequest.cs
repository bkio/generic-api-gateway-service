 /// MIT License, Copyright Burak Kara, burak@burak.io, https://en.wikipedia.org/wiki/MIT_License

using System;
using System.Collections.Generic;
using System.Net;
using BCommonUtilities;
using BWebServiceUtilities;
using ServiceUtilities.All;
using Newtonsoft.Json.Linq;

namespace ApiGatewayService.Endpoints
{
    internal class HandleRequest : BppWebServiceBase
    {
        private readonly string DestinationBaseUrl;
        private readonly string RootPath;

        public HandleRequest(string _ApiBaseUrl, string _RootPath)
        {
            DestinationBaseUrl = _ApiBaseUrl.TrimEnd('/');
            RootPath = _RootPath;
        }
        
        private string AuthServiceBaseUrl;
        private bool bAuthCheck = false;
        public HandleRequest WithLoginRequirement(string _AuthServiceBaseUrl)
        {
            AuthServiceBaseUrl = _AuthServiceBaseUrl.TrimEnd('/');
            bAuthCheck = true;

            return this;
        }
        private string Authenticated_UserID;
        private string Authenticated_UserName;
        private string Authenticated_UserEmail;
        private string Authenticated_AuthMethodKey;

        protected override BWebServiceResponse OnRequestPP(HttpListenerContext _Context, Action<string> _ErrorMessageAction = null)
        {
            GetTracingService()?.On_FromClientToGateway_Received(_Context, _ErrorMessageAction);
            
            var Result = OnRequest_Internal(_Context, _ErrorMessageAction);

            GetTracingService()?.On_FromGatewayToClient_Sent(_Context, _ErrorMessageAction);

            return Result;
        }

        private BWebServiceResponse OnRequest_Internal(HttpListenerContext _Context, Action<string> _ErrorMessageAction = null)
        {
            if (!AccessCheck(
                out BWebServiceResponse AuthFailureResponse,
                out bool bSSOTokenRefreshed, 
                out string NewSSOTokenAfterRefresh,
                _Context, 
                _ErrorMessageAction)) return AuthFailureResponse;

            GetTracingService()?.On_FromGatewayToService_Sent(_Context, _ErrorMessageAction);
            var Result = BWebServiceExtraUtilities.RequestRedirection(
                _Context,
                DestinationBaseUrl + _Context.Request.RawUrl,
                _ErrorMessageAction,
                bAuthCheck,
                false,
                new string[] { "do-not-get-db-clearance", "internal-call-secret" });
            GetTracingService()?.On_FromServiceToGateway_Received(_Context, _ErrorMessageAction);

            if (bSSOTokenRefreshed)
            {
                Result.Headers.Remove("x-bkio-sso-token-refreshed");
                Result.Headers.Add("x-bkio-sso-token-refreshed", new List<string>() { "true" });

                Result.Headers.Remove("x-bkio-sso-token-after-refresh");
                Result.Headers.Add("x-bkio-sso-token-after-refresh", new List<string>() { NewSSOTokenAfterRefresh });
            }

            return Result;
        }
        
        private bool AccessCheck(out BWebServiceResponse _FailureResponse, out bool _bSSOTokenRefreshed, out string _NewSSOTokenAfterRefresh, HttpListenerContext _Context, Action<string> _ErrorMessageAction = null)
        {
            _FailureResponse = new BWebServiceResponse();

            _bSSOTokenRefreshed = false;
            _NewSSOTokenAfterRefresh = "";

            if (bAuthCheck)
            {
                //Check for authorization header
                if (!BWebUtilities.DoesContextContainHeader(out List<string> Authorization, out string _, _Context, "authorization"))
                {
                    _FailureResponse = BWebResponse.Unauthorized("Authorization header must be set.");
                    return false;
                }

                var RequestObject = new JObject()
                {
                    ["forUrlPath"] = _Context.Request.RawUrl,
                    ["requestMethod"] = _Context.Request.HttpMethod
                };
                if (BUtility.CheckAndGetFirstStringFromList(Authorization, out string _Authorization))
                {
                    RequestObject["authorization"] = _Authorization;
                }
                else //Zero length
                {
                    _FailureResponse = BWebResponse.Unauthorized("Authorization header must be set.");
                    return false;
                }

                GetTracingService()?.On_FromGatewayToService_Sent(_Context, _ErrorMessageAction);
                var Result = BWebServiceExtraUtilities.InterServicesRequest(new BWebServiceExtraUtilities.InterServicesRequestRequest()
                {
                    DestinationServiceUrl = AuthServiceBaseUrl + RootPath + "auth/access_check",
                    RequestMethod =  "POST",
                    ContentType = "application/json",
                    Content = new BStringOrStream(RequestObject.ToString()),
                    bWithAuthToken = bAuthCheck,
                    UseContextHeaders = _Context,
                },
                false,
                _ErrorMessageAction);
                GetTracingService()?.On_FromServiceToGateway_Received(_Context, _ErrorMessageAction);

                if (!Result.bSuccess || Result.ResponseCode >= 400)
                {
                    if (Result.ResponseCode == BWebResponse.Error_Unauthorized_Code
                        || Result.ResponseCode == BWebResponse.Error_Forbidden_Code)
                    {
                        _FailureResponse = new BWebServiceResponse(Result.ResponseCode, Result.Content, Result.ContentType);
                        return false;
                    }

                    _ErrorMessageAction?.Invoke("Access check internal call has failed: Response: " + Result.ResponseCode + " -> " + Result.Content.String + ", Request: " + RequestObject.ToString());
                    _FailureResponse = BWebResponse.InternalError("Internal access check call has failed.");
                    return false;
                }

                var ResponseContent = Result.Content.ToString();
                try
                {
                    var Parsed = JObject.Parse(ResponseContent);
                    Authenticated_UserID = (string)Parsed["userId"];
                    Authenticated_UserName = (string)Parsed["userName"];
                    Authenticated_UserEmail = (string)Parsed["userEmail"];
                    Authenticated_AuthMethodKey = (string)Parsed["authMethodKey"];
                    _bSSOTokenRefreshed = (bool)Parsed["ssoTokenRefreshed"];
                    _NewSSOTokenAfterRefresh = (string)Parsed["newSSOTokenAfterRefresh"];
                }
                catch (Exception e)
                {
                    _ErrorMessageAction?.Invoke("HandleRequest->AccessCheck: Error during content parse: " + ResponseContent + ", Message: " + e.Message + ", Trace: " + e.StackTrace);
                    _FailureResponse = BWebResponse.InternalError("Request has failed due to an internal api gateway error.");
                    return false;
                }

                _Context.Request.Headers.Set("authorized-u-id", Authenticated_UserID);
                _Context.Request.Headers.Set("authorized-u-name", Authenticated_UserName);
                _Context.Request.Headers.Set("authorized-u-email", Authenticated_UserEmail);
                _Context.Request.Headers.Set("authorized-u-auth-key", Authenticated_AuthMethodKey);
            }
            return true;
        }
    }
}