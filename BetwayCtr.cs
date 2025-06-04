using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EVClientBot.Model;
using MasterDevs.ChromeDevTools;
using MasterDevs.ChromeDevTools.Protocol.Chrome.Page;
using MasterDevs.ChromeDevTools.Protocol.Chrome.DOM;
using MasterDevs.ChromeDevTools.Protocol.Chrome.Input;
using MasterDevs.ChromeDevTools.Protocol.Chrome.Network;
using Microsoft.Extensions.Logging;
using EVClientBot.Constants;
using System.Net.Http;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace EVClientBot.Control
{
    class BetwayCtr
    {
        protected onWriteStatusEvent m_handlerWriteStatus;
        protected onWriteLogEvent m_handlerWriteLog;
        public CookieContainer m_cookieContainer;
        private string m_domain = "https://betway.com/en/sports/";
        public string m_token = "";

        public SubBookieAccount m_Account;
        public List<List<JsonHorse>> betedHorseList = new List<List<JsonHorse>>();
        public ChromeDevCtr _chromeDevCtr = null;
        public HttpClient m_httpClient = null;
        public IChromeSession _chromeSession = null;

        private string _deviceId = "";
        private string _visitId = "";
        private string _sessionId = "";
        private string _JourneyId = "";

        private string _eventID = "";
        private string _betSlipData = "";
        private string _placeBetData = "";
        private string _lookupData = "";
        public JObject localStorageSavedjson = null;
        public JObject sessionStorageSavedjson = null;
        public string SessionId = string.Empty;
        public string CorrelationId = string.Empty;
        private bool bPageLoad = false;
        private string PageContent = "";
        private string _strPageUrl = "";

        public BetwayCtr(SubBookieAccount acount, onWriteStatusEvent onWriteStatus, onWriteLogEvent onWriteLog)
        {
            m_Account = acount;
            m_handlerWriteStatus = onWriteStatus;
            m_handlerWriteLog = onWriteLog;
            m_cookieContainer = new CookieContainer();
            betedHorseList = new List<List<JsonHorse>>();
        }
        public void closeBrowser()
        {

        }

        public double getBalance()
        {
            double balance = -1;
            try
            {
                //m_handlerWriteStatus($"SessionId:{SessionId}");
                //m_handlerWriteStatus($"CorrelationId:{CorrelationId}");
                string requestJson = $"{{\"IncludeAccountCapabilities\":false,\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"135.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"{SessionId}\",\"TerritoryId\":227,\"CorrelationId\":\"{CorrelationId}\",\"VisitId\":\"{_visitId}\",\"ViewName\":\"sports\",\"JourneyId\":\"{_JourneyId}\"}}";
                string eventURL = $"https://sportsapi.betway.com/api/Account/v3/Info";
                var postData = new StringContent(requestJson, Encoding.UTF8, "application/json");
                HttpResponseMessage balanceResponse = m_httpClient.PostAsync(eventURL, postData).Result;
                balanceResponse.EnsureSuccessStatusCode();
                string responseString = balanceResponse.Content.ReadAsStringAsync().Result;
                JObject originalObject = JObject.Parse(responseString);
                if (originalObject["Success"].ToString() == "True")
                {
                    balance = Convert.ToDouble(originalObject["CustomerInfo"]["Balance"]);
                    m_handlerWriteStatus("Current balance:" + balance.ToString() + "£");
                }
            }
            catch (Exception ex)
            {
                m_handlerWriteStatus("getbalance exception error:" + ex.Message);
            }
            return balance;
        }

        public List<BetHistoryModel> getSettedHistory(string username, out double dBalance, out bool ErrorStatus)
        {
            List<BetHistoryModel> lisselectBets = new List<BetHistoryModel>();
            dBalance = 0;
            ErrorStatus = true;
            DateTime today = new DateTime(2025, 5, 30);  // you can also use DateTime.Today

            // Extract year, month, and day
            int year = today.Year;
            int month = today.Month;
            int day = today.Day;

            // Create fixed time part: 05:24:56.500
            TimeSpan fixedTime = new TimeSpan(5, 24, 56); // hours, minutes, seconds
            int milliseconds = 500;

            // Create DateTime objects for today and yesterday with fixed time
            DateTime dateToday = new DateTime(year, month, day, fixedTime.Hours, fixedTime.Minutes, fixedTime.Seconds, DateTimeKind.Utc).AddMilliseconds(milliseconds);
            DateTime dateYesterday = dateToday.AddDays(-1);

            // Format to ISO 8601 string with milliseconds and UTC
            string dateTodayStr = dateToday.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string dateYesterdayStr = dateYesterday.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            try
            {
                string requestJson = $"{{\"BetsOffset\":0,\"BetsLimit\":11,\"BetHistoryType\":1,\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"CashOut\":false,\"BalanceType\":null,\"From\":\"{dateYesterdayStr}\",\"To\":\"{dateTodayStr}\",\"EventId\":null,\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"137.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"{SessionId}\",\"TerritoryId\":227,\"CorrelationId\":\"{CorrelationId}\",\"VisitId\":\"{_visitId}\",\"ViewName\":\"sports\",\"JourneyId\":\"{_JourneyId}\"}}";
                string eventURL = $"https://sportsapi.betway.com/api/Betting/v3/GetBetHistory";
                var postData = new StringContent(requestJson, Encoding.UTF8, "application/json");
                HttpResponseMessage bethistory = m_httpClient.PostAsync(eventURL, postData).Result;
                bethistory.EnsureSuccessStatusCode();
                string responseStr = bethistory.Content.ReadAsStringAsync().Result;
                JObject objContent = JObject.Parse(responseStr);
                JArray itemArry = JsonConvert.DeserializeObject<JArray>(objContent["Items"].ToString());
                foreach (JObject ItemObj in itemArry)
                {
                    try
                    {
                        if (ItemObj["Status"].ToString() == "2")
                        {
                            BetHistoryModel historyModel = new BetHistoryModel();
                            historyModel.UserName = username;
                            historyModel.BetslipId = ItemObj["ExternalId"].ToString();

                            double returnValue = Convert.ToDouble(ItemObj["PayoutAmount"].ToString()) / 100;
                            returnValue = Utils.ParseToDouble(returnValue.ToString("F2"));

                            if (returnValue > 0)
                            {
                                historyModel.Result = "WON";
                            }
                            else
                            {
                                historyModel.Result = "LOST";
                            }
                            historyModel.Return = returnValue;
                            lisselectBets.Add(historyModel);
                        }
                    }
                    catch (Exception ex) { }
                }
            }
            catch (Exception ex)
            {
                m_handlerWriteStatus("[Betway] Select History Error, Reason -> " + ex.Message);
                ErrorStatus = false;
            }
            dBalance = getBalance();
            return lisselectBets;
        }

        public bool betRace(List<JsonHorse> lisHorse, int nBetHorseNumber, ScheduleModel scheduleModel, out List<JsonHorse> betHorses, out List<JsonHorse> failHorses, out string ErrorMessage, out double dbalance, out BetHistoryModel historyModel)
        {
            betHorses = new List<JsonHorse>();
            failHorses = new List<JsonHorse>();
            ErrorMessage = "";
            dbalance = 0;
            historyModel = new BetHistoryModel();
            string startDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") + "T23:00:00.000Z";
            string endDate = DateTime.Now.ToString("yyyy-MM-dd") + "T22:59:59.999Z";
            try
            {
                string requestJson = "{\"CorrelationId\":\"" + Guid.NewGuid().ToString() + "\",\"ClientIntegratorId\":1,\"JourneyId\":\"" + _JourneyId + "\",\"TerritoryId\":227,\"BrowserId\":3,\"BrowserVersion\":\"112.0.0.0\",\"BrandId\":3,\"ClientTypeId\":2,\"JurisdictionId\":1,\"LanguageId\":1,\"CategoryCName\":\"horse-racing\",\"IncludedCountryCNames\":[],\"ExcludedCountryCNames\":[],\"MeetingCName\":null,\"MaxRaces\":0,\"FromUtcDate\":\"" + startDate + "\",\"ToUtcDate\":\"" + endDate + "\",\"IncludeVirtualSports\":false}";
                string eventURL = $"https://sportsapi.betway.com/api/Races/v1/GetRaces";
                var postData = new StringContent(requestJson, Encoding.UTF8, "application/json");
                HttpResponseMessage getRace = m_httpClient.PostAsync(eventURL, postData).Result;
                getRace.EnsureSuccessStatusCode();
                string responseStr = getRace.Content.ReadAsStringAsync().Result;

                JObject objContent = JObject.Parse(responseStr);
                JArray eventArry = JsonConvert.DeserializeObject<JArray>(objContent["Events"].ToString());
                for (int i = 0; i < lisHorse.Count; i++)
                {
                    foreach (JObject eventObj in eventArry)
                    {
                        try
                        {
                            if (eventObj["CategoryName"].ToString() != "Horse Racing" || eventObj["SubCategoryName"].ToString() != "UK & Ireland")
                                continue;

                            string eventInfo = eventObj["EventName"].ToString();
                            string eventTime = eventInfo.Split(' ')[0];
                            string eventName = eventInfo.Replace(eventTime, "").TrimStart().TrimEnd();

                            if (lisHorse[i].Meet.ToLower().Contains(eventName.ToLower()) && lisHorse[i].Track == eventTime)
                            {
                                lisHorse[i].EventId = eventObj["Id"].ToString();
                                break;
                            }
                        }
                        catch (Exception ex) { }
                    }
                }

                m_handlerWriteStatus("[BoyleSports] Adding BetSlip");
                int nAgainCount = 0;
            againAddSlip: int nBetCount = 0;

                while (nBetCount < nBetHorseNumber)
                {
                    try
                    {
                        Random random = new Random();
                        int nRdm = random.Next(0, lisHorse.Count - 1);
                        JsonHorse horseItem = lisHorse[nRdm];

                        if (horseItem.EventId != "")
                        {
                            _eventID = horseItem.EventId;
                            //_strPageUrl = "https://sportsapi.betway.com/api/Events/v2/GetRaceDetails";
                            bPageLoad = false;
                            PageContent = "";

                            string requestJson1 = $"{{\"EventId\":{_eventID},\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"ScoreboardRequest\":{{\"ScoreboardType\":3,\"IncidentRequest\":{{}}}},\"RacerMetaData\":[1],\"RaceMetaData\":[1],\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"137.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"{SessionId}\",\"TerritoryId\":227,\"CorrelationId\":\"{CorrelationId}\",\"VisitId\":\"{_visitId}\",\"ViewName\":\"sports\",\"JourneyId\":\"{_JourneyId}\"}}";
                            string GetRaceDetailsURL = $"https://sportsapi.betway.com/api/Events/v2/GetRaceDetails";
                            var postData1 = new StringContent(requestJson1, Encoding.UTF8, "application/json");
                            HttpResponseMessage GetRaceDetails = m_httpClient.PostAsync(GetRaceDetailsURL, postData1).Result;
                            GetRaceDetails.EnsureSuccessStatusCode();
                            string responseStr1 = GetRaceDetails.Content.ReadAsStringAsync().Result;

                            JObject raceObj = JObject.Parse(responseStr1);
                            JArray outComeArry = JsonConvert.DeserializeObject<JArray>(raceObj["Outcomes"].ToString());
                            foreach (JObject horseObj in outComeArry)
                            {
                                try
                                {
                                    string horseName = horseObj["BetName"].ToString();
                                    if (horseItem.Horse.Replace("'", "").ToLower() != horseName.ToLower().Replace("'", ""))
                                        continue;

                                    horseItem.HorseId = horseObj["Id"].ToString();
                                    horseItem.marketId = horseObj["MarketId"].ToString();
                                    horseItem.BetfredOdd = (double)horseObj["OddsDecimal"];

                                    if (horseItem.BetfredOdd < horseItem.Odds)
                                    {
                                        lisHorse.Remove(horseItem);
                                    }

                                    nBetCount++;
                                    betHorses.Add(horseItem);
                                    break;
                                }
                                catch (Exception ex) { }
                            }
                        }
                        else
                        {
                            failHorses.Add(horseItem);
                        }

                    removeHorse: lisHorse.Remove(horseItem);
                        if (lisHorse.Count < nBetHorseNumber - nBetCount)
                        {
                            m_handlerWriteStatus("[Betway]be short of the number of races");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {

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
                        m_handlerWriteStatus("[Oddsking]Error6" + "There is no new race set");
                        ErrorMessage = "There is no new race set";
                        return false;
                    }
                    goto againAddSlip;
                }

                string strOutComeData = "";
                for (int i = 0; i < betHorses.Count; i++)
                {
                    JsonHorse horseItem = betHorses[i];
                    strOutComeData = strOutComeData + "{\"EventId\":" + horseItem.EventId + ",\"MarketId\":" + horseItem.marketId + ",\"BetSelectionTypeId\":1,\"OutcomeIds\":[" + horseItem.HorseId + "],\"BalanceTypes\":[{\"Type\":\"cash\",\"Value\":\"\"}]},";
                }
                strOutComeData = strOutComeData.Substring(0, strOutComeData.Length - 1);

                _betSlipData = "{\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"IncludeStakeLimits\":false,\"BuildBetsRequestData\":[" + strOutComeData + "],\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"112.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"" + SessionId + "\",\"TerritoryId\":227,\"CorrelationId\":\"" + Guid.NewGuid().ToString() + "\",\"VisitId\":\"" + _visitId + "\",\"ViewName\":\"sports\",\"JourneyId\":\"" + _JourneyId + "\"}";

                //Send BetSlip Request
                _strPageUrl = "https://sportsapi.betway.com/api/Betting/v3/BuildBets";
                bPageLoad = false;
                PageContent = "";
                //string requestJson2 = $"{{\"EventId\":{_eventID},\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"ScoreboardRequest\":{{\"ScoreboardType\":3,\"IncidentRequest\":{{}}}},\"RacerMetaData\":[1],\"RaceMetaData\":[1],\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"137.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"{SessionId}\",\"TerritoryId\":227,\"CorrelationId\":\"{CorrelationId}\",\"VisitId\":\"{_visitId}\",\"ViewName\":\"sports\",\"JourneyId\":\"{_JourneyId}\"}}";
                //string BuildBetsURL = $"https://sportsapi.betway.com/api/Events/v2/GetRaceDetails";
                var postData2 = new StringContent(_betSlipData, Encoding.UTF8, "application/json");
                HttpResponseMessage BuildBets = m_httpClient.PostAsync(_strPageUrl, postData2).Result;
                BuildBets.EnsureSuccessStatusCode();
                string responseStr2 = BuildBets.Content.ReadAsStringAsync().Result;

                int numberLine = 0;
                string strBettingType = getBetTypeCode(scheduleModel, out numberLine);

                JObject slipObj = JObject.Parse(responseStr2);
                if ((bool)slipObj["Success"] != true)
                {
                    m_handlerWriteStatus(PageContent);
                    m_handlerWriteStatus("BetSlip Error");
                    return false;
                }

                double betStake = scheduleModel.stake * 100;

                JArray betTypeArry = JsonConvert.DeserializeObject<JArray>(slipObj["Accumulators"].ToString());
                JArray outcomeArry = JsonConvert.DeserializeObject<JArray>(slipObj["OutcomeDetails"].ToString());

                string outcomeIdList = "";

                dynamic betPlaceModel = new JObject();

                foreach (JObject betTypeObj in betTypeArry)
                {
                    try
                    {
                        if (betTypeObj["SystemCName"].ToString() != strBettingType || (int)betTypeObj["NumberOfLines"] != numberLine)
                            continue;

                        betPlaceModel["StakePerLine"] = int.Parse(betStake.ToString());
                        betPlaceModel["NumberOfLinesEachWay"] = (int)betTypeObj["NumberOfLinesEachWay"];
                        betPlaceModel["NumberOfLines"] = (int)betTypeObj["NumberOfLines"];
                        betPlaceModel["UseFreeBet"] = false;
                        if (scheduleModel.ew == (int)EachWayStatus.Yes)
                        {
                            betPlaceModel["EachWay"] = true;
                        }
                        else
                        {
                            betPlaceModel["EachWay"] = false;
                        }
                        betPlaceModel["PriceNumerator"] = 0;
                        betPlaceModel["PriceDenominator"] = 0;
                        betPlaceModel["BetSelectionTypeId"] = 0;
                        betPlaceModel["SystemCname"] = betTypeObj["SystemCName"].ToString();

                        string strSelection = "";
                        JArray selectionArry = JsonConvert.DeserializeObject<JArray>(betTypeObj["Selections"].ToString());
                        foreach (JObject selectionObj in selectionArry)
                        {
                            try
                            {
                                string outcomeID = selectionObj["SubSelections"][0]["OutcomeId"].ToString();
                                outcomeIdList = outcomeIdList + outcomeID + ",";

                                dynamic horseData = new JObject();
                                horseData["PriceDecimal"] = (double)selectionObj["PriceDecimal"];
                                horseData["EventStartDateMiliseconds"] = 0;
                                horseData["EventId"] = (long)selectionObj["EventId"];
                                horseData["MarketId"] = (long)selectionObj["MarketId"];
                                horseData["Handicap"] = 0;
                                horseData["PriceDenominator"] = (int)selectionObj["PriceDenominator"];
                                horseData["PriceNumerator"] = (int)selectionObj["PriceNumerator"];
                                horseData["CashOutActive"] = false;

                                foreach (JObject outcomeObj in outcomeArry)
                                {
                                    try
                                    {
                                        if (outcomeObj["OutcomeId"].ToString() == outcomeID)
                                        {
                                            horseData["EventName"] = outcomeObj["EventName"].ToString();
                                            horseData["MarketName"] = outcomeObj["MarketName"].ToString();
                                            horseData["MarketCName"] = "outright";

                                            string subSelection = "[{\"OutcomeId\":" + outcomeID + ",\"OutcomeName\":\"" + outcomeObj["OutcomeName"].ToString() + "\"}]";

                                            horseData["SubSelections"] = JsonConvert.DeserializeObject<JArray>(subSelection);
                                            break;
                                        }
                                    }
                                    catch (Exception ex) { }
                                }

                                horseData["PriceType"] = 1;
                                horseData["PriceDecimalDisplay"] = selectionObj["PriceDecimalDisplay"].ToString();
                                if (scheduleModel.ew == (int)EachWayStatus.Yes)
                                {
                                    horseData["EachWayActive"] = true;
                                    horseData["EachWayPosition"] = (int)selectionObj["EachWayPosition"];
                                    horseData["EachWayFractionDenominator"] = (int)selectionObj["EachWayFractionDenominator"];
                                }
                                else
                                {
                                    horseData["EachWayActive"] = false;
                                }

                                strSelection = strSelection + JsonConvert.SerializeObject(horseData) + ",";
                            }
                            catch (Exception ex) { }
                        }

                        strSelection = "[" + strSelection.Substring(0, strSelection.Length - 1) + "]";
                        betPlaceModel["Selections"] = JsonConvert.DeserializeObject<JArray>(strSelection);
                        break;

                    }
                    catch (Exception ex) { }
                }

            retrybet: string strBetRequestId = Guid.NewGuid().ToString().ToLower();
                _placeBetData = "{\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"BetsRequestData\":{\"AcceptPriceChange\":1,\"BetPlacements\":[" + JsonConvert.SerializeObject(betPlaceModel) + "]},\"BetRequestId\":\"" + strBetRequestId + "\",\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"112.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"" + SessionId + "\",\"TerritoryId\":227,\"CorrelationId\":\"" + Guid.NewGuid().ToString() + "\",\"VisitId\":\"" + _visitId + "\",\"ViewName\":\"sports\",\"JourneyId\":\"" + _JourneyId + "\"}";

                _strPageUrl = "https://sportsapi.betway.com/api/Betting/v3/InitiateBets";
                bPageLoad = false;
                PageContent = "";
                var postData3 = new StringContent(_placeBetData, Encoding.UTF8, "application/json");
                HttpResponseMessage InitiateBets = m_httpClient.PostAsync(_strPageUrl, postData3).Result;
                InitiateBets.EnsureSuccessStatusCode();
                string responseStr3 = InitiateBets.Content.ReadAsStringAsync().Result;

                JObject placeObj = JObject.Parse(responseStr3);
                if ((bool)placeObj["Success"] == true)
                {
                    _lookupData = "{\"LanguageId\":1,\"ClientTypeId\":2,\"BrandId\":3,\"JurisdictionId\":1,\"ClientIntegratorId\":1,\"BetRequestId\":\"" + strBetRequestId + "\",\"OutcomeIds\":[" + outcomeIdList.Substring(0, outcomeIdList.Length - 1) + "],\"BrowserId\":3,\"OsId\":3,\"ApplicationVersion\":\"\",\"BrowserVersion\":\"112.0.0.0\",\"OsVersion\":\"NT 10.0\",\"SessionId\":\"" + SessionId + "\",\"TerritoryId\":227,\"CorrelationId\":\"" + Guid.NewGuid().ToString() + "\",\"VisitId\":\"" + _visitId + "\",\"ViewName\":\"sports\",\"JourneyId\":\"" + _JourneyId + "\"}";
                    _strPageUrl = "https://sportsapi.betway.com/api/Betting/v3/LookupBets";
                    var postData4 = new StringContent(_lookupData, Encoding.UTF8, "application/json");
                    HttpResponseMessage LookupBets = m_httpClient.PostAsync(_strPageUrl, postData4).Result;
                    LookupBets.EnsureSuccessStatusCode();
                    string responseStr4 = LookupBets.Content.ReadAsStringAsync().Result;

                    JObject objData = JObject.Parse(responseStr4);
                    if (objData["BetStatus"].ToString() == "3" && (bool)objData["Success"])
                    {
                        string strPattern = "\"ExternalId\":\"(?<val>[^\"]*)\"";
                        string betId = Regex.Match(PageContent, strPattern).Groups["val"].Value;

                        betedHorseList.Add(betHorses);
                        dbalance = getBalance();

                        historyModel.Bookie = "Betway";
                        historyModel.BetslipId = betId;
                        historyModel.NumberOfLine = numberLine.ToString();
                        historyModel.OddsDecimal = "";

                        double totalStake = (double)objData["BetPlacementData"][0]["NumberOfLines"] * (double)objData["BetPlacementData"][0]["StakePerLine"] / 100;
                        if (scheduleModel.ew == (int)EachWayStatus.Yes)
                        {
                            historyModel.EachWay = "True";
                        }
                        else
                        {
                            historyModel.EachWay = "False";
                        }
                        historyModel.Stake = Convert.ToDouble(totalStake.ToString("F2"));
                        historyModel.Return = 0;
                        historyModel.Result = "N/A";
                        historyModel.BetType = strBettingType;
                        historyModel.UserName = scheduleModel.odds_username;

                        string horseInfo = "";
                        for (int i = 0; i < betHorses.Count; i++)
                        {
                            horseInfo = horseInfo + betHorses[i].Track + " " + betHorses[i].Meet + " " + betHorses[i].Horse + " @ " + betHorses[i].Odds + "\r\n";
                        }
                        historyModel.HorseInfo = horseInfo;
                        return true;
                    }
                    else
                    {
                        m_handlerWriteStatus(PageContent);
                        JObject errorObj = JsonConvert.DeserializeObject<JObject>(objData["Errors"][0].ToString());
                        if (errorObj["Message"].ToString() == "Your stake exceeds the maximum bet amount allowed.")
                        {
                            string strPattern = "\"MaxBet\":(?<val>[^,]*),";
                            int maxBet = int.Parse(Regex.Match(PageContent, strPattern).Groups["val"].Value);

                            if (maxBet > 0)
                            {
                                m_handlerWriteStatus("try max bet => " + maxBet.ToString());
                                betPlaceModel["StakePerLine"] = int.Parse(maxBet.ToString());
                                goto retrybet;
                            }
                        }
                    }
                }
                else
                {
                    m_handlerWriteStatus(PageContent);

                    ErrorMessage = "Place Bet Response Error";
                    return false;
                }
            }
            catch (Exception ex)
            {
                m_handlerWriteStatus("[Betway] Betting process fail, Reason -> " + ex.Message);
                ErrorMessage = ex.Message;
                return false;
            }
            return false;
        }

        private string getBetTypeCode(ScheduleModel scheduleModel, out int numberLine)
        {
            string strCheckPat = "";
            numberLine = 0;
            if (scheduleModel.bet_type == (int)BetType.Lucky15)
            {
                strCheckPat = "lucky-15";
                numberLine = 15;
            }
            else if (scheduleModel.bet_type == (int)BetType.Double)
            {
                strCheckPat = "double";
                numberLine = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Patent)
            {
                strCheckPat = "patent";
                numberLine = 7;
            }
            else if (scheduleModel.bet_type == (int)BetType.Treble)
            {
                strCheckPat = "treble";
                numberLine = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Trixie) 
            {
                strCheckPat = "trixie";
                numberLine = 4;
            }
            else if (scheduleModel.bet_type == (int)BetType.Yankee)
            {
                strCheckPat = "yankee";
                numberLine = 11;
            }
            else if (scheduleModel.bet_type == (int)BetType.Single)
            {
                strCheckPat = "single";
                numberLine = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Lucky31)
            {
                strCheckPat = "lucky-31";
                numberLine = 31;
            }
            else if (scheduleModel.bet_type == (int)BetType.fold4)
            {
                strCheckPat = "accumulator-4";
                numberLine = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.fold5)
            {
                strCheckPat = "accumulator-5";
                numberLine = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.fold6)
            {
                strCheckPat = "accumulator-6";
                numberLine = 1;
            }
            else if (scheduleModel.bet_type == (int)BetType.Lucky63)
            {
                strCheckPat = "lucky-63";
                numberLine = 63;
            }
            else if (scheduleModel.bet_type == (int)BetType.Heinz)
            {
                strCheckPat = "heinz";
            }
            else if (scheduleModel.bet_type == (int)BetType.SuperHeinz)
            {
                strCheckPat = "SUPER_HEINZ";
            }
            else if (scheduleModel.bet_type == (int)BetType.fold7)
            {
                strCheckPat = "accumulator-7";
                numberLine = 1;
            }

            return strCheckPat;
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
        // Fix for CS0103: The name 'cookies' does not exist in the current context  
        // The variable 'cookies' is not defined in the current context.  
        // Assuming 'cookies' should be a collection of CookieParam objects, we need to define it.  

        private List<CookieParam> cookies = new List<CookieParam>(); // Add this field to the class.

        public bool doLogin()
        {
            _JourneyId = Guid.NewGuid().
                ToString().ToLower();
            bool bLogin = false;
            try
            {
                _chromeDevCtr = new ChromeDevCtr(m_handlerWriteStatus, m_handlerWriteLog, _chromeSession);
                _chromeDevCtr.InitializeBrowser();
                _chromeDevCtr._chromeSession.SendAsync(new NavigateCommand
                {
                    Url = "https://betway.com/en/sports"
                }).Wait();
                Thread.Sleep(17000);

                long documentId = _chromeDevCtr._chromeSession.SendAsync(new GetDocumentCommand()).Result.Result.Root.NodeId;
                bool isFoundLogoutbutton = _chromeDevCtr.FindElement(documentId, "div[class='accountUsername']").Result;
                if (isFoundLogoutbutton)
                {
                    bLogin = true;
                    return bLogin;
                }

                bool PolicyFound = _chromeDevCtr.FindElement(documentId, "button[data-testid='dialog-button-2']").Result;                
                if (!PolicyFound)
                {
                    Thread.Sleep(5000);
                }
                bool isFound = _chromeDevCtr.FindAndClickElement(documentId, "button[data-testid='dialog-button-2']", 3).Result;
                Thread.Sleep(2000);
                if (isFound)
                {
                    m_handlerWriteStatus("Clicked Cookie button");
                }
                isFound = _chromeDevCtr.FindAndClickElement(documentId, "input[placeholder='Username']", 3).Result;
                _chromeDevCtr.InputText(m_Account.UserName);
                Thread.Sleep(1000);
                if (isFound)
                {
                    m_handlerWriteStatus("inputed Username");
                }
                isFound = _chromeDevCtr.FindAndClickElement(documentId, "input[placeholder='Password']", 3).Result;
                _chromeDevCtr.InputText(m_Account.Password);
                Thread.Sleep(1000);
                if (isFound)
                {
                    m_handlerWriteStatus("inputed password");
                }
                isFound = _chromeDevCtr.FindAndClickElement(documentId, "div[class='loginButton button submitButton']", 1).Result;
                Thread.Sleep(7000);
                if (isFound)
                {
                    m_handlerWriteStatus("clicked submit button");
                }
                GetAllCookiesCommandResponse resp = _chromeDevCtr._chromeSession.SendAsync(new GetAllCookiesCommand()).Result.Result;
                foreach (MasterDevs.ChromeDevTools.Protocol.Chrome.Network.Cookie cookie in resp.Cookies)
                {
                    try
                    {
                        System.Net.Cookie http_cookie = new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain);
                        m_cookieContainer.Add(http_cookie);

                        // Populate the 'cookies' list with CookieParam objects.
                        cookies.Add(new CookieParam
                        {
                            Name = cookie.Name,
                            Value = cookie.Value,
                            Domain = cookie.Domain,
                            Path = cookie.Path,
                            Secure = cookie.Secure,
                            HttpOnly = cookie.HttpOnly,
                            SameSite = CookieSameSite.None, // Assuming default value.
                            Expires = cookie.Expires ?? 0, // Assuming default value.
                            Priority = CookiePriority.Medium // Assuming default value.
                        });
                    }
                    catch (Exception ex) { }
                }

                foreach (CookieParam _cookie in cookies)
                {
                    try
                    {
                        if (_cookie.Name == "SpinSportVisitId")
                        {
                            _visitId = _cookie.Value;
                        }
                        else if (_cookie.Name == "ssc_DeviceId")
                        {
                            _deviceId = _cookie.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        string d = "";
                    }
                }

                m_handlerWriteStatus("VisitId => " + _visitId);
                m_handlerWriteStatus("DeviceID => " + _deviceId);
                if (isFoundLogoutbutton = _chromeDevCtr.FindElement(documentId, "div[class='accountUsername']").Result)
                {
                    bLogin = true;
                    SessionId = _chromeDevCtr.sessionID;
                    CorrelationId = _chromeDevCtr.CorrelationId;

                }
                _chromeDevCtr.Close_Browser();
                getHttpClient();
            }
            catch (Exception e)
            {
                m_handlerWriteStatus("betway login ecxception error:" + e.Message);
            }
            return bLogin;
        }

        private void getHttpClient()
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate

            };

            handler.CookieContainer = m_cookieContainer;
            m_httpClient = new HttpClient(handler);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            m_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json; charset=UTF-8");
            m_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.90 Safari/537.36");

            m_httpClient.DefaultRequestHeaders.ExpectContinue = false;

        }

    }
}
