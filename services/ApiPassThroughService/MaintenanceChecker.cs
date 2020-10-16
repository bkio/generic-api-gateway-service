/// MIT License, Copyright Burak Kara, burak@burak.io, https://en.wikipedia.org/wiki/MIT_License

using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using BCommonUtilities;

namespace ApiPassThroughService
{
    class MaintenanceChecker
    {
        private MaintenanceChecker() {}
        private static MaintenanceChecker Instance = null;
        public static MaintenanceChecker Get()
        {
            if (Instance == null)
            {
                Instance = new MaintenanceChecker();
            }
            return Instance;
        }

        private string MaintenanceModeCheckUrl;
        private Action<string> ErrorMessageAction = null;

        public void Start(string _MaintenanceModeCheckUrl, Action<string> _ErrorMessageAction = null)
        {
            MaintenanceModeCheckUrl = _MaintenanceModeCheckUrl;
            ErrorMessageAction = _ErrorMessageAction;

            BTaskWrapper.Run(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    do
                    {
                        CheckMaintenanceMode();

                        Thread.Sleep(2500);

                    } while (true);
                }
                catch (Exception) { }
            });
        }

        private void CheckMaintenanceMode()
        {
            using var Handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true
            };

            using var Client = new HttpClient(Handler);
            try
            {
                using var RequestTask = Client.GetAsync(MaintenanceModeCheckUrl);
                RequestTask.Wait();

                using var Response = RequestTask.Result;
                using var Content = Response.Content;
                using var ReadResponseTask = Content.ReadAsStringAsync();
                ReadResponseTask.Wait();

                bMaintenanceModeOn = ReadResponseTask.Result.Trim('\n', '\r', ' ').ToLower() == "on";
                return;
            }
            catch (Exception e)
            {
                ErrorMessageAction?.Invoke("Error: CheckMaintenanceMode: " + e.Message + ", trace: " + e.StackTrace);
            }
            bMaintenanceModeOn = false;
        }

        public bool IsMaintenanceModeOn()
        {
            return bMaintenanceModeOn;
        }
        private bool bMaintenanceModeOn = false;
    }
}