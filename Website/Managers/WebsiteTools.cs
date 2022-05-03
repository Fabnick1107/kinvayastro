﻿using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Genso.Astrology.Library;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace Website
{
    public delegate Task AsyncEventHandler();
    
    /// <summary>
    /// Simple class holding general functions used in project
    /// </summary>
    public static class WebsiteTools
    {

        //░█▀▀▀ ▀█▀ ░█▀▀▀ ░█─── ░█▀▀▄ ░█▀▀▀█ 　 ─█▀▀█ ░█▄─░█ ░█▀▀▄ 　 ░█▀▀█ ░█▀▀█ ░█▀▀▀█ ░█▀▀█ ░█▀▀▀█ 
        //░█▀▀▀ ░█─ ░█▀▀▀ ░█─── ░█─░█ ─▀▀▀▄▄ 　 ░█▄▄█ ░█░█░█ ░█─░█ 　 ░█▄▄█ ░█▄▄▀ ░█──░█ ░█▄▄█ ─▀▀▀▄▄ 
        //░█─── ▄█▄ ░█▄▄▄ ░█▄▄█ ░█▄▄▀ ░█▄▄▄█ 　 ░█─░█ ░█──▀█ ░█▄▄▀ 　 ░█─── ░█─░█ ░█▄▄▄█ ░█─── ░█▄▄▄█


        /// <summary>
        /// Event fired just after user has signed in
        /// </summary>
        public static event AsyncEventHandler OnUserSignIn;


        /// <summary>
        /// Event fired just after user has signed out
        /// </summary>
        public static event AsyncEventHandler OnUserSignOut;

        /// <summary>
        /// Is true when only when Google Sign In success
        /// False when Google Sign Out success
        /// </summary>
        public static bool GoogleUserSignedIn { get; set; }


        /// <summary>
        /// Default User ID for all before they sign in
        /// </summary>
        public const string PublicUserId = "101";




        //░█▀▄▀█ ░█▀▀▀ ▀▀█▀▀ ░█─░█ ░█▀▀▀█ ░█▀▀▄ ░█▀▀▀█ 
        //░█░█░█ ░█▀▀▀ ─░█── ░█▀▀█ ░█──░█ ░█─░█ ─▀▀▀▄▄ 
        //░█──░█ ░█▄▄▄ ─░█── ░█─░█ ░█▄▄▄█ ░█▄▄▀ ░█▄▄▄█


        public static async Task<dynamic> GetAddressLocation(string address)
        {
            //create the request url for Google API
            var url = $"https://maps.googleapis.com/maps/api/geocode/xml?key={ServerManager.GoogleGeoLocationApiKey}&address={Uri.EscapeDataString(address)}&sensor=false";

            //get location data from GoogleAPI
            var rawReplyXml = await ServerManager.ReadFromServer(url);

            //extract out the longitude & latitude
            var locationData = new XDocument(rawReplyXml);
            var result = locationData.Element("GeocodeResponse").Element("result");
            var locationElement = result.Element("geometry").Element("location");
            var lat = Double.Parse(locationElement.Element("lat").Value);
            var lng = Double.Parse(locationElement.Element("lng").Value);

            //round coordinates to 3 decimal places
            lat = Math.Round(lat, 3);
            lng = Math.Round(lng, 3);

            //get full name with country & state
            var fullName = result.Element("formatted_address").Value;

            return new { FullName = fullName, Latitude = lat, Longitude = lng };

        }


        /// <summary>
        /// gets the name of the place given th coordinates, uses Google API
        /// </summary>
        public static async Task<string> CoordinateToAddress(decimal longitude, decimal latitude)
        {
            //create the request url for Google API
            var url = string.Format($"https://maps.googleapis.com/maps/api/geocode/xml?latlng={latitude},{longitude}&key={ServerManager.GoogleGeoLocationApiKey}");


            //get location data from GoogleAPI
            var rawReplyXml = await ServerManager.ReadFromServer(url);

            //extract out the longitude & latitude
            var locationData = new XDocument(rawReplyXml);
            var localityResult = locationData.Element("GeocodeResponse")?.Elements("result").FirstOrDefault(result => result.Element("type")?.Value == "locality");
            var locationName = localityResult?.Element("formatted_address")?.Value;


            return locationName;

        }


        /// <summary>
        /// Tries to ID the user, and sends a log of the visit to API server
        /// Called from MainLayout everytime page is loaded
        /// </summary>
        public static async Task LogVisitor(IJSRuntime jsRuntime)
        {
            //get url user is on
            var urlXml = new XElement("Url", await jsRuntime.InvokeAsync<string>("getUrl"));

            //find out if new visitor just arriving or old one browsing
            var uniqueId = await GetVisitorIdFromCookie();
            var isNewVisitor = uniqueId == null;

            //based on visitor write the log
            //this is done to minimize excessive logging
            if (isNewVisitor) { await NewVisitor(); }
            else { await OldVisitor(); }


            //-------------- FUNCTIONS -------------------------

            //all possible details are logged
            async Task NewVisitor()
            {

                //get visitor data & format it nicely for storage
                var browserDataXml = await GetBrowserDataXml();
                var timeStampXml = new XElement("TimeStamp", Tools.GetNowSystemTimeText());
                var visitorId = Tools.GenerateId();
                var uniqueIdXml = new XElement("UniqueId", visitorId);
                var locationXml = await ServerManager.ReadFromServer(ServerManager.GetGeoLocation, "Location");
                var visitorElement = new XElement("Visitor");
                visitorElement.Add(uniqueIdXml, urlXml, timeStampXml, locationXml, browserDataXml);

                //send to API for save keeping
                var result = await ServerManager.WriteToServer(ServerManager.AddVisitorApi, visitorElement);

                //mark visitor with id inside cookie
                await SetNewVisitorIdInCookie(visitorId);

                //check result, display error if needed
                if (result.Value != "Pass") { Console.WriteLine($"BLZ: ERROR: Add Visitor Api\n{result.Value}"); }
            }

            //only needed details are logged
            async Task OldVisitor()
            {

                //get visitor data & format it nicely for storage
                var visitorElement = new XElement("Visitor");
                var timeStampXml = new XElement("TimeStamp", Tools.GetNowSystemTimeText());
                var uniqueIdXml = new XElement("UniqueId", uniqueId); //use id generated above
                visitorElement.Add(uniqueIdXml, urlXml, timeStampXml);

                //send to API for save keeping
                var result = await ServerManager.WriteToServer(ServerManager.AddVisitorApi, visitorElement);

                //check result, display error if needed
                if (result.Value != "Pass") { Console.WriteLine($"BLZ: ERROR: Add Visitor Api\n{result.Value}"); }
            }

            //returns null if no id found
            async Task<string> GetVisitorIdFromCookie() => await jsRuntime.InvokeAsync<string>("getCookiesWrapper", "uniqueId");

            async Task SetNewVisitorIdInCookie(string id) => await jsRuntime.InvokeVoidAsync("setCookiesWrapper", "uniqueId", id);

            //calls js library to get browser data, converts it to xml
            async Task<XElement> GetBrowserDataXml()
            {
                var dataJson = await jsRuntime.InvokeAsync<JsonNode>("getVisitorData");
                var rawXml = JsonConvert.DeserializeXmlNode(dataJson.ToString(), "BrowserData");
                return XElement.Parse(rawXml.InnerXml);
            }

        }


        /// <summary>
        /// This method is called from JS when user signs in
        /// </summary>
        [JSInvokable]
        public static void InvokeOnUserSignIn()
        {
            //remember user signed in
            GoogleUserSignedIn = true;
            //let others know
            OnUserSignIn?.Invoke();
        }


        /// <summary>
        /// This method is called from JS when user signs out
        /// </summary>
        [JSInvokable]
        public static void InvokeOnUserSignOut()
        {
            //remember user signed out
            GoogleUserSignedIn = false;
            //let others know
            OnUserSignOut?.Invoke();
        }


        /// <summary>
        /// Tries to get user login state, else returns public user id.
        /// Note: Public User Id is the standard id all unregistered visitors get
        /// </summary>
        public static async Task<string> GetUserIdAsync(IJSRuntime jsRuntime)
        {
            //wait here a little if user has not signed in
            //5 X 200 delay = 1 sec wait time max
            var signInSuccess = await WaitTillGoogleSignInSuccess(5, 200);

            //if sign in failed use Public Id
            if (!signInSuccess) { Console.WriteLine("BLZ: GetUserIdAsync: Public ID Assigned"); return PublicUserId; }

            //get user Id from variable in JS scope
            var userId = await jsRuntime.InvokeAsync<string>("getGoogleUserIdToken");

            return userId;
        }


        /// <summary>
        /// if try limit is expired then returns false
        /// try limit 5 X 200 delay = 1 sec wait time max
        /// </summary>
        public static async Task<bool> WaitTillGoogleSignInSuccess(int tryLimit, int delay)
        {
            var count = 0;
            while (!GoogleUserSignedIn && count < tryLimit)
            {
                Console.WriteLine("BLZ: GetUserIdAsync: Waiting For Sign In");
                await Task.Delay(delay);
                count++;
            }

            if (!GoogleUserSignedIn && count == tryLimit) {  return false; }

            //if control reaches here than, user sign in success
            return true;
        }

        /// <summary>
        /// Gets all people list from API server
        /// </summary>
        public static async Task<List<Person>> GetPeopleList(string userId)
        {
            var personListRootXml = await ServerManager.WriteToServer(ServerManager.GetPersonListApi, new XElement("UserId", userId));
            var personList = personListRootXml.Elements().Select(personXml => Person.FromXml(personXml)).ToList();
            return personList;
        }

        public static async Task<List<Person>> GetMalePeopleList(string userId)
        {
            var rawMaleListXml = await ServerManager.WriteToServer(ServerManager.GetMaleListApi, new XElement("UserId", userId));
            return rawMaleListXml.Elements().Select(maleXml => Person.FromXml(maleXml)).ToList();
        }

        public static async Task<List<Person>> GetFemalePeopleList(string userId)
        {
            var rawMaleListXml = await ServerManager.WriteToServer(ServerManager.GetFemaleListApi, new XElement("UserId", userId));
            return rawMaleListXml.Elements().Select(maleXml => Person.FromXml(maleXml)).ToList();
        }

        /// <summary>
        /// Gets person instance from name contacts API
        /// Note: uses API to get latest data
        /// </summary>
        public static async Task<Person> GetPersonFromHash(string personHash)
        {
            //send newly created person to API server
            var xmlData = Tools.AnyTypeToXml(personHash);
            var result = await ServerManager.WriteToServer(ServerManager.GetPersonApi, xmlData);

            var personXml = result.Element("Person");

            //parse received person
            var receivedPerson = Person.FromXml(personXml);

            return receivedPerson;
        }

        public static async Task DeletePerson(int personHash)
        {
            var personHashXml = new XElement("PersonHash", personHash);
            var result = await ServerManager.WriteToServer(ServerManager.DeletePersonApi, personHashXml);

            //check result, display error if needed
            if (result.Value != "Pass") { Console.WriteLine($"BLZ: ERROR: Delete Person API\n{result.Value}"); }
        }

        public static async Task UpdatePerson(Person person, int originalPersonHash)
        {
            var updatedPersonXml = person.ToXml();
            var oriPersonHashXml = new XElement("PersonHash", originalPersonHash);
            var rootXml = new XElement("Root");
            rootXml.Add(oriPersonHashXml, updatedPersonXml);
            var result = await ServerManager.WriteToServer(ServerManager.UpdatePersonApi, rootXml);

            //check result, display error if needed
            if (result.Value != "Pass") { Console.WriteLine($"BLZ: ERROR: Update Person API\n{result.Value}"); }

        }

        public static void ReloadPage(NavigationManager navigation) => navigation.NavigateTo(navigation.Uri, forceLoad: true);
    }
}
