using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EVClientBot.Model;
using EVClientBot.Model.BetfredJson;
using EVClientBot.Constants;

using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace EVClientBot.Control
{
    class BetfredCtr
    {
        protected onWriteStatusEvent m_handlerWriteStatus;
        protected onWriteLogEvent m_handlerWriteLog;

        public SubBookieAccount m_Account;

        public List<List<JsonHorse>> betedHorseList = new List<List<JsonHorse>>();
        public HttpClient m_httpClient = null;
        public CookieContainer m_cookieContainer;

        public string m_Token = "";
        public int PageNumber = 1;

        public BetfredCtr(SubBookieAccount acount, onWriteStatusEvent onWriteStatus, onWriteLogEvent onWriteLog)
        {
            m_Account = acount;
            m_handlerWriteStatus = onWriteStatus;
            m_handlerWriteLog = onWriteLog;
            betedHorseList = new List<List<JsonHorse>>();
            m_Token = "";
        }

        private bool CheckBetted(List<JsonHorse> newList)
        {
            List<BetHorseClass> lisNewHorse = new List<BetHorseClass>();
            for (int i = 0; i < newList.Count; i++)
            {
                BetHorseClass item = new BetHorseClass(newList[i], false);
                lisNewHorse.Add(item);
            }

            bool b_lastStatu = false;
            for (int i = 0; i < betedHorseList.Count; i++)
            {
                List<JsonHorse> betedList = betedHorseList[i];
                for (int j = 0; j < lisNewHorse.Count; j++)
                {
                    lisNewHorse[j].statu = false;
                    for (int k = 0; k < betedList.Count; k++)
                    {
                        if (lisNewHorse[j].horse.Horse == betedList[k].Horse && lisNewHorse[j].horse.Track == betedList[k].Track && lisNewHorse[j].horse.Meet == betedList[k].Meet)
                        {
                            lisNewHorse[j].statu = true;
                        }
                    }
                }

                bool bStatu = true;
                for (int j = 0; j < lisNewHorse.Count; j++)
                {
                    if (lisNewHorse[j].statu == false)
                    {
                        bStatu = false;
                        break;
                    }
                }

                if (bStatu)
                {
                    b_lastStatu = true;
                    break;
                }
            }

            if (b_lastStatu)
            {
                return true;   // there is same race list
            }
            else
            {
                return false; // there is no same race list
            }
        }

        private void getHttpClient()
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            handler.CookieContainer = m_cookieContainer;
            m_httpClient = new HttpClient(handler);
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            m_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            m_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36");
            m_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            m_httpClient.DefaultRequestHeaders.ExpectContinue = false;
        }

        public Browser _browserFetcher;
        private Page _workPage;
        private bool bPageLoad = false;
        private string PageContent = "";
        private string _strPageUrl = "";

        private string strPostParam = "";
        private string strEventId = "";

        private bool bResponseError = false;

        public bool doLogin()
        {
            m_cookieContainer = new CookieContainer();
            m_cookieContainer.PerDomainCapacity = 100;
            closeBrowser();
            bool bLogin = false;

            try
            {
                _browserFetcher = Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    IgnoreHTTPSErrors = true,
                    Args = new[] {
                    "--no-sandbox",
                    "--disable-infobars",
                    "--disable-setuid-sandbox",
                    "--ignore-certificate-errors"
                },
                }).Result;

                _workPage = _browserFetcher.NewPageAsync().Result;
                //_workPage.AuthenticateAsync(new Credentials() { Username = Setting.instance.ProxyUser, Password = Setting.instance.ProxyPass });
                _workPage.EvaluateExpressionOnNewDocumentAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined })").Wait();
                _workPage.SetJavaScriptEnabledAsync(true).Wait();       
                _workPage.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36").Wait();
                _workPage.SetRequestInterceptionAsync(true).Wait();

                _workPage.Console += async (sender, e) =>
                {
                };

                _workPage.Response += async (sender, e) =>
                {
                    try
                    {
                        if (_strPageUrl.Contains(e.Response.Url))
                        {
                            bPageLoad = true;
                            PageContent = await e.Response.TextAsync();
                        }

                        bResponseError = false;
                    }
                    catch(Exception ex) 
                    {
                        bResponseError = true;
                    }
                };

                _workPage.Request += async (sender, e) =>
                {
                    try
                    {
                        if (e.Request.Url.Contains("sgp-api.betfred.com/api-standalone/1/auth/login?language_code=en&franchise_id=3&company_id=2"))
                        {
                            #region Login Param
                            Dictionary<string, string> heards = new Dictionary<string, string>();
                            heards.Add("Accept", "application/json, text/plain, */*");
                            heards.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                            heards.Add("Content-Type", "application/json");
                            heards.Add("Referer", "https://www.betfred.com/");
                            heards.Add("X-Correlation-Id", Guid.NewGuid().ToString().ToLower());
                            heards.Add("X-Current-Url", "https://www.betfred.com/");
                            heards.Add("X-Device-Type", "Desktop");
                            heards.Add("X-Native-App", "false");
                            heards.Add("X-Platform-Name", "Windows");
                            heards.Add("X-Platform-Version", "10");
                            heards.Add("X-Page-Id", "1");
                            heards.Add("Origin", "https://www.betfred.com");

                            string iobToken = getIOBToken();
                            string content = "{\"username\":\"" + m_Account.UserName + "\",\"password\":\"" + m_Account.Password + "\",\"fraudCharacteristics\":{\"deviceRecord\":\"" + iobToken + "\",\"fraudCheckError\":{\"errors\":[],\"deviceInfo\":{\"deviceType\":\"Desktop\",\"deviceModel\":null,\"deviceBrand\":null,\"browserName\":\"Chrome\",\"browserVersion\":\"120.0.0.0\",\"platformName\":\"Windows\",\"platformVersion\":\"10\"},\"fraudCheckEnabled\":true}}}";

                            var payLoad = new Payload()
                            {
                                Headers = heards,
                                Method = HttpMethod.Post,
                                PostData = content,
                            };
                            await e.Request.ContinueAsync(payLoad);
                            #endregion
                        }
                        else if (e.Request.Url.Contains("sgp-api.betfred.com/api-standalone/1/account/basicBalance?"))
                        {
                            #region Balance Param
                            Dictionary<string, string> heards = new Dictionary<string, string>();
                            heards.Add("Accept", "application/json, text/plain, */*");
                            heards.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                            heards.Add("Content-Type", "application/json");
                            heards.Add("Referer", "https://www.betfred.com/");
                            heards.Add("X-Device-Type", "Desktop");
                            heards.Add("X-Native-App", "false");
                            heards.Add("X-Platform-Name", "Windows");
                            heards.Add("X-Platform-Version", "10");
                            heards.Add("Authorization", "Bearer " + m_Token);
                            heards.Add("Origin", "https://www.betfred.com");

                            var payLoad = new Payload()
                            {
                                Headers = heards,
                                Method = HttpMethod.Get
                            };
                            await e.Request.ContinueAsync(payLoad);
                            #endregion
                        }
                        else if (e.Request.Url.Contains("sgp-api.betfred.com/api-standalone/1/bet/history?company_id=2&franchise_id=3&language_code=en"))
                        {
                            #region History Param
                            Dictionary<string, string> heards = new Dictionary<string, string>();
                            heards.Add("Accept", "application/json, text/plain, */*");
                            heards.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                            heards.Add("Content-Type", "application/json");
                            heards.Add("Referer", "https://www.betfred.com/");
                            heards.Add("X-Device-Type", "Desktop");
                            heards.Add("X-Native-App", "false");
                            heards.Add("X-Platform-Name", "Windows");
                            heards.Add("X-Platform-Version", "10");
                            heards.Add("Authorization", "Bearer " + m_Token);
                            heards.Add("Origin", "https://www.betfred.com");

                            string today = DateTime.Now.ToString("yyyy-MM-dd");
                            string yesterday = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd");
                            string strPostData = "{\"dateFrom\":\"" + yesterday + "T00:00:00.000Z\",\"dateTo\":\"" + today + "T23:59:59.999Z\",\"liveOnly\":false,\"betIds\":[],\"pageSize\":10,\"pageNumber\":" + PageNumber.ToString() + ",\"betStatus\":5}";

                            var payLoad = new Payload()
                            {
                                Headers = heards,
                                Method = HttpMethod.Post,
                                PostData = strPostData,
                            };
                            await e.Request.ContinueAsync(payLoad);
                            #endregion
                        }
                        else if (e.Request.Url.Contains("sgp-api.betfred.com/api-standalone/1/bet/sgpCheckBet"))
                        {
                            #region BetSlip Param
                            Dictionary<string, string> heards = new Dictionary<string, string>();
                            heards.Add("Accept", "application/json, text/plain, */*");
                            heards.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                            heards.Add("Content-Type", "application/json");
                            heards.Add("Referer", "https://www.betfred.com/");
                            heards.Add("Origin", "https://www.betfred.com");
                            heards.Add("X-Correlation-Id", Guid.NewGuid().ToString().ToLower());
                            heards.Add("X-Current-Url", "https://www.betfred.com/sports/horse-racing/event/" + strEventId);
                            heards.Add("X-Device-Type", "Desktop");
                            heards.Add("X-Native-App", "false");
                            heards.Add("X-Page-Id", "1");
                            heards.Add("X-Platform-Name", "Windows");
                            heards.Add("X-Platform-Version", "10");
                            heards.Add("Authorization", "Bearer " + m_Token);

                            var payLoad = new Payload()
                            {
                                Headers = heards,
                                Method = HttpMethod.Post,
                                PostData = strPostParam,
                            };
                            await e.Request.ContinueAsync(payLoad);
                            #endregion
                        }
                        else if (e.Request.Url.Contains("sgp-api.betfred.com/api-standalone/1/bet/placebet"))
                        {
                            #region Placebet Param
                            Dictionary<string, string> heards = new Dictionary<string, string>();
                            heards.Add("Accept", "application/json, text/plain, */*");
                            heards.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                            heards.Add("Content-Type", "application/json");
                            heards.Add("Referer", "https://www.betfred.com/");
                            heards.Add("Origin", "https://www.betfred.com");
                            heards.Add("X-Correlation-Id", Guid.NewGuid().ToString().ToLower());
                            heards.Add("X-Current-Url", "https://www.betfred.com/sports/horse-racing/event/" + strEventId);
                            heards.Add("X-Device-Type", "Desktop");
                            heards.Add("X-Native-App", "false");
                            heards.Add("X-Page-Id", "1");
                            heards.Add("X-Platform-Name", "Windows");
                            heards.Add("X-Platform-Version", "10");
                            heards.Add("Authorization", "Bearer " + m_Token);

                            var payLoad = new Payload()
                            {
                                Headers = heards,
                                Method = HttpMethod.Post,
                                PostData = strPostParam,
                            };
                            await e.Request.ContinueAsync(payLoad);
                            #endregion
                        }
                        else
                        {
                            await e.Request.ContinueAsync();
                        }
                    }catch(Exception ex)
                    {

                    }
                };

                string strPageUrl = "https://www.betfred.com/";
                var strPage = _workPage.GoToAsync(strPageUrl);
                Thread.Sleep(5000);

                _workPage.ReloadAsync();

                bool bCookieShow = false; // Cookie Click check
                int nRetry = 5;
                while (nRetry > 0)
                {
                    try
                    {
                        var cookieEle = _workPage.XPathAsync("//a[@class='wscrOk']");
                        if (cookieEle != null && cookieEle.Result.Length > 0)
                        {
                            ElementHandle element = cookieEle.Result[0];
                            element.ClickAsync().Wait();
                            bCookieShow = true;
                            Thread.Sleep(1000);
                            break;
                        }
                    }
                    catch (Exception ex) { }
                    Thread.Sleep(10 * 1000);
                    nRetry--;
                }
                Thread.Sleep(5000);

                bPageLoad = false;
                PageContent = "";
                _strPageUrl = "https://sgp-api.betfred.com/api-standalone/1/auth/login?language_code=en&franchise_id=3&company_id=2";
                strPage = _workPage.GoToAsync(_strPageUrl);
                Thread.Sleep(3000);

                bool bflag = bcheckPageLoad();

                JObject objContent = JObject.Parse(PageContent);
                bLogin = (bool)objContent["success"];

                if (bLogin)
                {
                    var cookies = _workPage.GetCookiesAsync().Result;
                    foreach (CookieParam _cookie in cookies)
                    {
                        try
                        {
                            System.Net.Cookie cookie1 = new System.Net.Cookie(_cookie.Name, _cookie.Value) { Domain = _cookie.Domain };
                            m_cookieContainer.Add(cookie1);
                        }
                        catch (Exception ex)
                        {
                            string d = "";
                        }
                    }

                    getHttpClient();
                }

                m_Token = objContent["token"].ToString();
                m_handlerWriteStatus("Token => " + m_Token);
            }
            catch(Exception ex)
            {
                m_handlerWriteStatus("Betfred Chrome Error => " + ex.Message);
            }
            return bLogin;
        }

        public void closeBrowser()
        {
            try
            {
                if (_workPage != null)
                {
                    _workPage.CloseAsync();
                }
            }
            catch (Exception ex) { }
            try
            {
                if (_browserFetcher != null)
                {
                    _browserFetcher.CloseAsync();
                }
            }
            catch (Exception ex) { }
        }

        private bool bcheckPageLoad()
        {
            bool bflag = true;
            int nTry = 20;
            while (!bPageLoad || string.IsNullOrEmpty(PageContent))
            {
                Thread.Sleep(1000);
                nTry--;
                if (nTry < 0 || bResponseError)
                {
                    bflag = false;
                    break;
                }
            }
            return bflag;
        }

        public string getIOBToken()
        {
            string iobToken = string.Empty;
            try
            {
                HttpResponseMessage tokenResponseMessage = m_httpClient.GetAsync("https://mpsnare.iesnare.com/script/logo.js").Result;
                tokenResponseMessage.EnsureSuccessStatusCode();

                string tokenResponseContent = tokenResponseMessage.Content.ReadAsStringAsync().Result;
                GroupCollection groups = Regex.Match(tokenResponseContent, "\"(.*?)\"").Groups;
                string token = groups[1].ToString();

                HttpResponseMessage snareResponseMessage = m_httpClient.GetAsync("https://mpsnare.iesnare.com/snare.js").Result;
                snareResponseMessage.EnsureSuccessStatusCode();

                string content = snareResponseMessage.Content.ReadAsStringAsync().Result;
                groups = Regex.Match(content, "\"JSSRC\",.*?\"(.*?)\"").Groups;
                string jssrc = groups[1].ToString();

                string jstime = DateTime.UtcNow.ToString("%yy/%M/%d %H:%m:%s");
                groups = Regex.Match(content, "\"SVRTIME\",.*?\"(.*?)\"").Groups;
                string svrtime = groups[1].ToString();

                groups = Regex.Match(content, "\"IGGY\",.*?\"(.*?)\"").Groups;
                string iggy = groups[1].ToString();

                groups = Regex.Match(content, "\"JSVER\",.*?\"(.*?)\"").Groups;
                string jsver = groups[1].ToString();

                string encodeStr = string.Format("00190006INTLOC{0}0004JINT0008function0005JENBL000110005JSSRC{1}0004UAGT0072Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.360007JSTOKEN{2}0007HACCLNG000een-US,en;q=0.90005JSVER{3}0004TZON{4}0006JSTIME{5}0007SVRTIME{6}0005JBRNM0006Chrome0005JBRVR000c67.0.3396.990005JBROS000fWindows NT 10.00005APVER006a5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.360005APNAM0008Netscape0005NPLAT0005Win320005JBRCM001dWin64; x64; KHTML, like Gecko0005JLANG0005en-US0004IGGY{7}0004JRES0008864x15360006JPLGNS004ainternal-pdf-viewer;mhjfbmdgcfjbbpaeojofohoefgiehjai;internal-nacl-plugin;0007LSTOKEN{8}0006CTOKEN{9}0008WDBTOKEN{10}", Utils.encode("https://www.betfred.com/"), Utils.encode(jssrc), Utils.encode(token), Utils.encode(jsver), Utils.encode("300"), Utils.encode(jstime), Utils.encode(svrtime), Utils.encode(iggy), Utils.encode(token), Utils.encode(token), Utils.encode(token));
                string path = Environment.CurrentDirectory + "\\iobbhelper.js";
                string jsContent = File.ReadAllText(path);
                using (ScriptEngine engine = new ScriptEngine("jscript"))
                {
                    ParsedScript parsed = engine.Parse(jsContent);
                    iobToken = parsed.CallMethod("getblackbox", encodeStr).ToString();
                }
            }
            catch (Exception ex)
            {

            }
            return iobToken;
        }


        public bool betRace(List<JsonHorse> lisHorse, int nBetHorseNumber, ScheduleModel scheduleModel, out List<JsonHorse> betHorses, out List<JsonHorse> failHorses, out string ErrorMessage, out double dbalance, out BetHistoryModel historyModel)
        {
            betHorses = new List<JsonHorse>();
            failHorses = new List<JsonHorse>();
            ErrorMessage = "";
            dbalance = 0;
            historyModel = new BetHistoryModel();
            try
            {
                _strPageUrl = "https://sdds-api.betfred.com/1/sport/60/racing-by-date/today?language_code=en&franchise_id=3&tz_offset=60";
                string strRacingUrl = "https://www.betfred.com/sports/horse-racing";

                bPageLoad = false;
                PageContent = "";
                _workPage.GoToAsync(strRacingUrl);

                Thread.Sleep(4000);

                bool bpageLoad = bcheckPageLoad();
                if (!bpageLoad)
                {
                    ErrorMessage = "Horse Page Roading Error";
                    return false;
                }

                JObject objContent = JObject.Parse(PageContent);
                JArray RaceContent = JsonConvert.DeserializeObject<JArray>(objContent["events"].ToString());
                
                m_handlerWriteStatus("[BetFred] Adding BetSlip");
                int nAgainCount = 0;
            againAddSlip: int nBetCount = 0;  //Added horse Number

                BetslipModel betslipModel = new BetslipModel();
                List<SelectionModel> lisSelections = new List<SelectionModel>();

                while (nBetCount < nBetHorseNumber)
                {
                    Random random = new Random();
                    int nRdm = random.Next(0, lisHorse.Count - 1);
                    JsonHorse horseItem = lisHorse[nRdm];

                    bPageLoad = false;
                    PageContent = "";
                    _workPage.GoToAsync(strRacingUrl);
                    Thread.Sleep(2000);
                    bpageLoad = bcheckPageLoad();

                    bool bGetValue = false;
                    foreach (JObject raceRecord in RaceContent)
                    {
                        if (raceRecord["countrySlug"].ToString() != "uk-ireland")
                            continue;

                        if (horseItem.Meet.ToLower().Contains(raceRecord["tournamentName"].ToString().ToLower()))
                        {
                            string raceTime = raceRecord["dateStart"].ToString();
                            raceTime = raceTime.Replace("T", " ").Replace(".000Z", "");

                            string strTime = DateTime.Parse(raceTime).AddHours(1).ToString("HH:mm");
                            //string strTime = DateTime.Parse(raceTime).ToString("HH:mm");
                            if (horseItem.Track != strTime)
                                continue;

                            string eventId = raceRecord["id"].ToString();

                            string eventUrl = string.Format("https://sdds-api.betfred.com/1/event/{0}?language_code=en&franchise_id=3&tz_offset=60", eventId);
                            horseItem.RaceUrl = eventUrl;

                            _strPageUrl = eventUrl;
                            bPageLoad = false;
                            PageContent = "";

                            try
                            {
                                // var raceEles = _workPage.XPathAsync("//a[@class='_1bnlx25']");
                                var raceEles = _workPage.XPathAsync("//a[@data-actionable='RaceGrid.eventLink']");
                                if (raceEles != null && raceEles.Result.Length > 0)
                                {
                                    foreach (ElementHandle raceEle in raceEles.Result)
                                    {
                                        try
                                        {
                                            string hrefValue = raceEle.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").Result;
                                            if (hrefValue.Contains(eventId))
                                            {
                                                raceEle.ClickAsync().Wait();
                                                Thread.Sleep(1000);
                                                break;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            m_handlerWriteStatus("Button Error => " + ex.Message);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                m_handlerWriteStatus("Button Error1 => " + ex.Message);
                            }
                            Thread.Sleep(3000);

                            bpageLoad = bcheckPageLoad();
                            if (!bpageLoad)
                            {
                                m_handlerWriteStatus("Event Load Error");
                                break;
                            }

                            JObject eventObj = JObject.Parse(PageContent);
                            JArray marketsArry = JsonConvert.DeserializeObject<JArray>(eventObj["markets"].ToString());
                            foreach (JObject marketObj in marketsArry)
                            {
                                try
                                {
                                    if (marketObj["marketName"].ToString() != "Winner")
                                        continue;

                                    bool bEachWay = (bool)marketObj["eachWay"]["enabled"];
                                    if (!bEachWay)
                                        break;

                                    JArray oddsArry = JsonConvert.DeserializeObject<JArray>(marketObj["odds"].ToString());
                                    foreach (JObject oddObj in oddsArry)
                                    {
                                        try
                                        {
                                            string horseName = oddObj["teamName"].ToString();
                                            if (horseName.ToLower().Replace("'", "") == horseItem.Horse.ToLower().Replace("'", ""))
                                            {
                                                horseItem.EventId = eventId;
                                                horseItem.IDFOMarket = oddObj["id"].ToString();
                                                horseItem.IDFOSelection = oddObj["eventTeamPlayersId"].ToString();
                                                horseItem.PriceUp = oddObj["numerator"].ToString();
                                                horseItem.PriceDown = oddObj["denominator"].ToString();
                                                horseItem.BetfredOdd = Convert.ToDouble(oddObj["oddValue"].ToString());

                                                bGetValue = true;
                                                break;
                                            }
                                        }
                                        catch (Exception ex) { }
                                    }

                                    break;
                                }
                                catch (Exception ex) { }
                            }

                            if (bGetValue)
                                break;
                        }
                    }


                    SelectionModel selectionModel = new SelectionModel();

                    if (horseItem.IDFOMarket != "" && horseItem.IDFOSelection != "" && horseItem.EventId != "")
                    {
                        if (horseItem.BetfredOdd >= horseItem.Odds)
                        {
                            selectionModel.eventId = Convert.ToInt32(horseItem.EventId);
                            selectionModel.eventOddID = Convert.ToInt32(horseItem.IDFOMarket);
                            selectionModel.eventTeamPlayersId = Convert.ToInt32(horseItem.IDFOSelection);

                            OddModel oddModel = new OddModel();
                            oddModel.numerator = int.Parse(horseItem.PriceUp);
                            oddModel.denominator = int.Parse(horseItem.PriceDown);
                            oddModel.decimalValue = horseItem.BetfredOdd;

                            selectionModel.odds = oddModel;

                            betslipModel.selections.Add(selectionModel);
                            lisSelections.Add(selectionModel);

                            nBetCount++;
                            betHorses.Add(horseItem);
                            lisHorse.Remove(horseItem);
                        }
                        else
                        {
                            failHorses.Add(horseItem);
                            lisHorse.Remove(horseItem);
                        }
                    }
                    else
                    {
                        lisHorse.Remove(horseItem);
                    }

                    if (lisHorse.Count < nBetHorseNumber - nBetCount)
                    {
                        m_handlerWriteStatus("[BetFred]be short of the number of races");
                        ErrorMessage = "be short of the number of horse";
                        return false;
                    }
                }

                bool bcheckStatus = CheckBetted(betHorses);
                if (bcheckStatus)
                {
                    nAgainCount++;
                    for (int i = 0; i < betHorses.Count; i++)
                    {
                        lisHorse.Add(betHorses[i]);
                    }
                    betHorses = new List<JsonHorse>();
                    if (nAgainCount > 4)
                    {
                        m_handlerWriteStatus("[BetFred]Error6" + "There is no new race set");
                        ErrorMessage = "There is no new race set";
                        return false;
                    }
                    goto againAddSlip;
                }

                try
                {
                    strEventId = betHorses[0].EventId;
                    strPostParam = JsonConvert.SerializeObject(betslipModel);

                    _strPageUrl = "https://sgp-api.betfred.com/api-standalone/1/bet/sgpCheckBet?language_code=en&franchise_id=3&company_id=2";
                    bPageLoad = false;
                    PageContent = "";
                    _workPage.GoToAsync(_strPageUrl);

                    Thread.Sleep(3000);

                    bpageLoad = bcheckPageLoad();
                    if (!bpageLoad)
                    {
                        ErrorMessage = "Event Load Error";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    m_handlerWriteStatus("Betslip Post Error => " + ex.Message);
                    return false;
                }

                JObject slipResultObj = JObject.Parse(PageContent);
                if ((bool)slipResultObj["success"] != true)
                {
                    m_handlerWriteStatus("Betslip Response Error");
                    m_handlerWriteStatus(PageContent);
                    return false;
                }

                JArray seletonArry = JsonConvert.DeserializeObject<JArray>(slipResultObj["selections"].ToString());

                List<SelectionModel1> seleArry = new List<SelectionModel1>();
                for (int i = 0; i < lisSelections.Count; i++)
                {
                    foreach (JObject selectionObj in seletonArry)
                    {
                        if (lisSelections[i].eventOddID.ToString() == selectionObj["eventOddID"].ToString())
                        {
                            lisSelections[i].id = selectionObj["id"].ToString();
                            JObject obj = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(lisSelections[i]));
                            SelectionModel1 model1 = obj.ToObject<SelectionModel1>();
                            model1.eachWayInfoId = Convert.ToInt64(selectionObj["availableEachWayInfo"][0]["id"].ToString());
                            seleArry.Add(model1);
                            break;
                        }
                    }
                }

                double stack = scheduleModel.stake;

                int NumberOfCombinations = 0;
                string strCheckType = getBetTypeCode(scheduleModel, out NumberOfCombinations);

                dynamic placebetModel = new JObject();
                placebetModel.selections = JsonConvert.DeserializeObject<JArray>(JsonConvert.SerializeObject(seleArry));
                placebetModel.source = 1;
                placebetModel.oddsChangeToleration = 3;
                placebetModel.guid = Guid.NewGuid().ToString().ToLower();

                dynamic betModel = new JObject();

                JArray betsArry = JsonConvert.DeserializeObject<JArray>(slipResultObj["bets"].ToString());
                foreach (JObject betTypeObj in betsArry)
                {
                    try
                    {
                        if (betTypeObj["typeName"].ToString() == strCheckType)
                        {
                            betModel.additionalInfo = JsonConvert.DeserializeObject<JObject>(betTypeObj["additionalInfo"].ToString());
                            betModel.betID = int.Parse(betTypeObj["betID"].ToString());
                            betModel.headlineOdds = JsonConvert.DeserializeObject<JObject>(betTypeObj["headlineOdds"].ToString());
                            betModel.minStake = Convert.ToDouble(betTypeObj["minStake"].ToString());
                            betModel.maxStake = Convert.ToDouble(betTypeObj["maxStake"].ToString());
                            betModel.maxWin = Convert.ToDouble(betTypeObj["maxWin"].ToString());
                            betModel.maxRequest = Convert.ToDouble(betTypeObj["maxRequest"].ToString());
                            betModel.maxExceedAction = betTypeObj["maxExceedAction"].ToString();
                            betModel.potentialReturnsMultiplierEachWay = Convert.ToDouble(betTypeObj["potentialReturnsMultiplierEachWay"].ToString());
                            betModel.potentialReturnsMultiplierWin = Convert.ToDouble(betTypeObj["potentialReturnsMultiplierWin"].ToString());
                            betModel.reviewReasons = 0;
                            betModel.type = betTypeObj["type"].ToString();
                            betModel.typeName = betTypeObj["typeName"].ToString();
                            betModel.code = betTypeObj["code"].ToString();
                            betModel.status = betTypeObj["status"].ToString();
                            betModel.eachWayAllowed = (bool)betTypeObj["eachWayAllowed"];
                            betModel.isForecast = (bool)betTypeObj["isForecast"];
                            break;
                        }
                    }
                    catch (Exception ex) { }
                }


                int retry = 0;
                while (retry < 5)
                {
                    double totalStake = 0;
                    if (scheduleModel.ew == (int)EachWayStatus.Yes)
                    {
                        betModel.isEachWay = true;
                        totalStake = stack * 2 * NumberOfCombinations;
                    }
                    else
                    {
                        betModel.isEachWay = false;
                        totalStake = stack * NumberOfCombinations;
                    }

                    if (totalStake < 0.5)
                    {
                        m_handlerWriteStatus("Total stake is small than 0.5, Bet Fail");
                        return false;
                    }
                retryBet:
                    betModel.stake = totalStake;
                    string strBetModel = JsonConvert.SerializeObject(betModel);
                    placebetModel.bets = JsonConvert.DeserializeObject<JArray>("[" + strBetModel + "]");

                    strPostParam = JsonConvert.SerializeObject(placebetModel);

                    _strPageUrl = "https://sgp-api.betfred.com/api-standalone/1/bet/placebet?language_code=en&franchise_id=3&company_id=2";
                    bPageLoad = false;
                    PageContent = "";
                    _workPage.GoToAsync(_strPageUrl);

                    Thread.Sleep(3000);

                    bpageLoad = bcheckPageLoad();
                    if (!bpageLoad)
                    {
                        ErrorMessage = "Place Bet Request Error";
                        return false;
                    }
                    
                    JObject placebetObj = JObject.Parse(PageContent);

                    if (placebetObj["validationStatus"].ToString() == "VALIDATION_OK" && (bool)placebetObj["success"])
                    {
                        betedHorseList.Add(betHorses);
                        dbalance = getBalance();

                        JArray Bets = JsonConvert.DeserializeObject<JArray>(placebetObj["bets"].ToString());

                        historyModel.Bookie = "Betfred";
                        historyModel.BetslipId = Bets[0]["betID"].ToString();
                        historyModel.NumberOfLine = "";
                        historyModel.OddsDecimal = "";
                        if (scheduleModel.ew == (int)EachWayStatus.Yes)
                        {
                            historyModel.EachWay = "True";
                        }
                        else
                        {
                            historyModel.EachWay = "False";
                        }
                        historyModel.Stake = Utils.ParseToDouble(Bets[0]["stake"].ToString());
                        historyModel.Return = 0;
                        historyModel.Result = "N/A";
                        historyModel.BetType = Bets[0]["typeName"].ToString();
                        historyModel.UserName = scheduleModel.odds_username;
                        
                        string horseInfo = "";
                        for (int i = 0; i < betHorses.Count; i++)
                        {
                            horseInfo = horseInfo + betHorses[i].Track + " " + betHorses[i].Meet + " " + betHorses[i].Horse + " @ " + betHorses[i].Odds + "\r\n"; ;
                        }
                        historyModel.HorseInfo = horseInfo;

                        return true;
                    }
                    else if(placebetObj["validationStatus"].ToString() == "VALIDATION_EXCEEDED_MAX_STAKE")
                    {
                        JArray Bets = JsonConvert.DeserializeObject<JArray>(placebetObj["bets"].ToString());
                        double maxStake = Utils.ParseToDouble(Bets[0]["maxStake"].ToString());

                        m_handlerWriteStatus("total Max Stake => " + maxStake.ToString());
                        m_handlerWriteStatus("Retry Bet");
                        totalStake = maxStake;
                        goto retryBet;
                    }
                    else
                    {
                        m_handlerWriteStatus(PageContent);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error2" + ex.Message;
            }
            return false;
        }

        private string getBetTypeCode(ScheduleModel scheduleModel, out int NumberOfCombinations)
        {
            NumberOfCombinations = 0;
            string strCheckPat = "";
            if (scheduleModel.bet_type == (int)BetType.Lucky15)
            {
                strCheckPat = "Lucky15";
                NumberOfCombinations = 15;
            }
            else if (scheduleModel.bet_type == (int)BetType.Double)
            {
                strCheckPat = "Double";
                NumberOfCombinations = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Patent)
            {
                strCheckPat = "Patent";
                NumberOfCombinations = 7;
            }
            else if (scheduleModel.bet_type == (int)BetType.Treble)
            {
                strCheckPat = "Treble";
                NumberOfCombinations = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Trixie)
            {
                strCheckPat = "Trixie";
                NumberOfCombinations = 4;
            }
            else if (scheduleModel.bet_type == (int)BetType.Yankee)
            {
                strCheckPat = "Yankee";
                NumberOfCombinations = 11;
            }
            else if (scheduleModel.bet_type == (int)BetType.Single)
            {
                strCheckPat = "Single";
                NumberOfCombinations = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Lucky31)
            {
                strCheckPat = "Lucky31";
                NumberOfCombinations = 31;
            }
            else if (scheduleModel.bet_type == (int)BetType.fold4)
            {
                strCheckPat = "Four_folds";
                NumberOfCombinations = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.fold5)
            {
                strCheckPat = "Five_folds";
                NumberOfCombinations = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.fold6)
            {
                strCheckPat = "Six_folds";
                NumberOfCombinations = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Lucky63)
            {
                strCheckPat = "Lucky63";
                NumberOfCombinations = 63;
            }
            else if (scheduleModel.bet_type == (int)BetType.Heinz)
            {
                strCheckPat = "HEINZ";
            }
            else if (scheduleModel.bet_type == (int)BetType.SuperHeinz)
            {
                strCheckPat = "SUPER_HEINZ";
            }
            else if (scheduleModel.bet_type == (int)BetType.fold7)
            {
                strCheckPat = "SEVENFOLD";
            }
            return strCheckPat;
        }

        public double getBalance()
        {
            double balance = -1;

            try
            {
                _strPageUrl = "https://sgp-api.betfred.com/api-standalone/1/account/basicBalance?company_id=2&franchise_id=3&language_code=en";
                bPageLoad = false;
                PageContent = "";
                _workPage.GoToAsync(_strPageUrl);
                Thread.Sleep(3000);

                bool bflag = bcheckPageLoad();
                if (!bflag)
                {
                    m_handlerWriteStatus("Balance Error1");
                    return balance;
                }

                JObject objContent = JObject.Parse(PageContent);
                balance = Convert.ToDouble(objContent["balance"]["availableToWithdraw"].ToString());
            }
            catch (Exception ex)
            {
                m_handlerWriteStatus("Balance Error2, Reason -> " + ex.Message);
            }
            return balance;
        }

        public List<BetHistoryModel> getSeleteHistory(string username, out double dBalance, out bool ErrorStatus)
        {
            List<BetHistoryModel> lisselectBets = new List<BetHistoryModel>();
            dBalance = 0;
            ErrorStatus = true;
            PageNumber = 1;
            try
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                string yesterday = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd");

                _strPageUrl = "https://sgp-api.betfred.com/api-standalone/1/bet/history?company_id=2&franchise_id=3&language_code=en";

                while (true)
                {
                    bPageLoad = false;
                    PageContent = "";
                    _workPage.GoToAsync(_strPageUrl);
                    bool bpageLoad = bcheckPageLoad();
                    if (!bpageLoad)
                    {
                        m_handlerWriteStatus(PageContent);
                        m_handlerWriteStatus("BetHistory Page Load Error");
                        m_handlerWriteStatus("Try Login");
                        ErrorStatus = false;
                        return lisselectBets;
                    }

                    JObject obj = JsonConvert.DeserializeObject<JObject>(PageContent);
                    if ((bool)obj["success"] == true)
                    {
                        JArray items = JsonConvert.DeserializeObject<JArray>(obj["bets"].ToString());
                        foreach (JObject item in items)
                        {
                            try
                            {
                                BetHistoryModel historyModel = new BetHistoryModel();
                                historyModel.UserName = username;

                                historyModel.BetslipId = item["betID"].ToString();
                                historyModel.Return = Utils.ParseToDouble(item["amountWon"].ToString());
                                if (historyModel.Return > 0)
                                {
                                    historyModel.Result = "WON";
                                }
                                else
                                {
                                    historyModel.Result = "LOST";
                                }
                                lisselectBets.Add(historyModel);
                            }
                            catch (Exception ex) { }
                        }

                        int totalPage = int.Parse(obj["totalPagesCount"].ToString());
                        bool blastPage = (bool)obj["lastPage"];

                        if (blastPage == true)
                        {
                            break;
                        }
                        else
                        {
                            PageNumber++;
                        }
                    }
                }

                dBalance = getBalance();
            }
            catch(Exception ex)
            {
                ErrorStatus = false;
            }
            return lisselectBets;

        }
    }
}
