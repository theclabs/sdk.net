using System;
using System.Web;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using RestSharp;
using System.Collections.Generic;

namespace MercadoLibre.SDK
{
	public class Meli
	{
		private RestClient client = new RestClient (ApiUrl);
		static private string apiUrl = "https://api.mercadolibre.com";
        static private string sdkVersion = "MELI-NET-SDK-0.0.1.2-mod_TeamTheClabs";
		static public string ApiUrl {
			get {
				return apiUrl;
			}
			set {
				apiUrl = value;
			}
		}

		public string ClientSecret { get; private set; }

		public long ClientId { get; private set; }

		public string AccessToken { get; private set; }

		public string RefreshToken { get; private set; }

        public String UserLogedId { get; private set; }

		public Meli (long clientId, string clientSecret)
		{
			this.ClientId = clientId;
			this.ClientSecret = clientSecret;
            this.UserLogedId = "1"; // se inicializa e 1 
		}

		public Meli (long clientId, string clientSecret, string accessToken)
		{
			this.ClientId = clientId;
			this.ClientSecret = clientSecret;
			this.AccessToken = accessToken;
		}

		public Meli (long clientId, string clientSecret, string accessToken, string refreshToken)
		{
			this.ClientId = clientId;
			this.ClientSecret = clientSecret;
			this.AccessToken = accessToken;
			this.RefreshToken = refreshToken;
		}

		public string GetAuthUrl (string redirectUri)
		{

			return "https://auth.mercadolibre.com.ar/authorization?response_type=code&client_id=" + ClientId + "&redirect_uri=" + HttpUtility.UrlEncode (redirectUri);
		}


        //Este metodo permite retornar el CODE para poder autorizar a la app a que 
        //pueda operar con el usuario logueado, retorna el identificador CODE
        //mediante el uso de un index.php que se encuentra en este caso en ../Code
        //<?PHP // index.php
        //    $var = $_GET['code'];
        //    print $var;           
        //?>
        public string GetCodeAuth() 
        {
                                                                                                 // app Id & Lugar donde se encuentra el index.php que retora el code
            String url = "https://auth.mercadolibre.com.ar/authorization?response_type=code&client_id=5550&redirect_uri=http://meli-theclabs.no-ip.info/jimaz/code/";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse respons = (HttpWebResponse)request.GetResponse();
            Stream response = respons.GetResponseStream();
            StreamReader streamread = new StreamReader((System.IO.Stream)response, System.Text.Encoding.Default);
            string sourceCode = streamread.ReadToEnd();
            streamread.Close();
            respons.Close();
            return sourceCode;
        }

        // obtiene token y demas datos enviando la autorizacion con el code.
		public void Authorize (string code, string redirectUri)
		{
			var request = new RestRequest ("/oauth/token?grant_type=authorization_code&client_id={client_id}&client_secret={client_secret}&code={code}&redirect_uri={redirect_uri}", Method.POST);

			request.AddParameter ("client_id", this.ClientId, ParameterType.UrlSegment);
			request.AddParameter ("client_secret", this.ClientSecret, ParameterType.UrlSegment);
			request.AddParameter ("code", code, ParameterType.UrlSegment);
			request.AddParameter ("redirect_uri", redirectUri, ParameterType.UrlSegment);

			request.AddHeader ("Accept", "application/json");

			var response = ExecuteRequest (request);

            // Verifica que la consulta se haya podido concretar
			if (response.StatusCode.Equals (HttpStatusCode.OK)) 
                {
				var token = JsonConvert.DeserializeAnonymousType (response.Content, new {refresh_token="", access_token = ""});
				this.AccessToken = token.access_token;
				this.RefreshToken = token.refresh_token;
                }
            // si no se pudo se envia el codigo de porque
            else if ((int)response.StatusCode != 200)
                {
                throw new AuthorizationException((int)response.StatusCode);
                }
            else
                {// en caso que e problema sea otro se controla aca.
                throw new AuthorizationException();
                }
		}

        // devuelve URL para desloguear el WebBrowser si es que usamo.
        public string GetDeauthUrl()
        {
            return "http://www.mercadolibre.com.ar/jm/logout";
        }

        //Elimina los tokens y refresh que halla en el sistema
        public void deAuth()
        {
            this.AccessToken = "destruido";
            this.RefreshToken = "destruido";
        }

		public IRestResponse Get (string resource)
		{
			return Get (resource, new List<Parameter> ());
		}

		void refreshToken ()
		{
			var request = new RestRequest ("/oauth/token?grant_type=refresh_token&client_id={client_id}&client_secret={client_secret}&refresh_token={refresh_token}", Method.POST);
			request.AddParameter ("client_id", this.ClientId, ParameterType.UrlSegment);
			request.AddParameter ("client_secret", this.ClientSecret, ParameterType.UrlSegment);
			request.AddParameter ("refresh_token", this.RefreshToken, ParameterType.UrlSegment);

			request.AddHeader ("Accept", "application/json");

			var response = ExecuteRequest (request);

			if (response.StatusCode.Equals (HttpStatusCode.OK)) {
				var token = JsonConvert.DeserializeAnonymousType (response.Content, new {refresh_token="", access_token = ""});
				this.AccessToken = token.access_token;
				this.RefreshToken = token.refresh_token;
			} else {
				throw new AuthorizationException ();
			}
		}

        // parsea el token para la busqueda de el ID del usuario que esta logueado. lo guarda y lo retorna
        public string getUserLoguedID(String token)
        {
            String test = token;
            int posGuion = test.LastIndexOf("-");
            posGuion++;
            test = test.Substring(posGuion, test.Length - posGuion);
            this.UserLogedId = test;  // guarda el ID en la clase, y retorna, siempre retorna.
            return test;
        }


		public IRestResponse Get (string resource, List<Parameter> param)
		{
			bool containsAT = false;

			var request = new RestRequest (resource, Method.GET);
			List<string> names = new List<string> ();
			foreach (Parameter p in param) {
				names.Add (p.Name + "={" + p.Name + "}");
				if (p.Name.Equals ("access_token")) {
					containsAT = true;
				}
				p.Type = ParameterType.UrlSegment;
				request.AddParameter (p);
			}

			request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

			request.AddHeader ("Accept", "application/json");

			var response = ExecuteRequest (request);

			if (!string.IsNullOrEmpty (this.RefreshToken) && response.StatusCode == HttpStatusCode.NotFound && containsAT) {
				refreshToken ();

				request = new RestRequest (resource, Method.GET);
				names = new List<string> ();
				foreach (Parameter p in param) {
					if (p.Name.Equals ("access_token")) {
						p.Value = this.AccessToken;
					}
					names.Add (p.Name + "={" + p.Name + "}");
					p.Type = ParameterType.UrlSegment;
					request.AddParameter (p);
				}

				request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

				request.AddHeader ("Accept", "application/json");

				response = ExecuteRequest (request);
			}

			return response;
		}

		public IRestResponse Post (string resource, List<Parameter> param, object body)
		{
			bool containsAT = false;

			var request = new RestRequest (resource, Method.POST);
			List<string> names = new List<string> ();
			foreach (Parameter p in param) {
				names.Add (p.Name + "={" + p.Name + "}");
				if (p.Name.Equals ("access_token")) {
					containsAT = true;
				}
				p.Type = ParameterType.UrlSegment;
				request.AddParameter (p);
			}

			request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

			request.AddHeader ("Accept", "application/json");
			request.AddHeader ("Content-Type", "application/json");
			request.RequestFormat = DataFormat.Json;

			request.AddBody (body);

			var response = ExecuteRequest (request);

			if (!string.IsNullOrEmpty (this.RefreshToken) && response.StatusCode == HttpStatusCode.NotFound && containsAT) {
				refreshToken ();

				request = new RestRequest (resource, Method.POST);
				names = new List<string> ();
				foreach (Parameter p in param) {
					if (p.Name.Equals ("access_token")) {
						p.Value = this.AccessToken;
					}
					names.Add (p.Name + "={" + p.Name + "}");
					p.Type = ParameterType.UrlSegment;
					request.AddParameter (p);
				}

				request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

				request.AddHeader ("Accept", "application/json");
				request.AddHeader ("Content-Type", "application/json");
				request.RequestFormat = DataFormat.Json;

				request.AddBody (body);
				response = ExecuteRequest (request);
			}

			return response;
		}

		public IRestResponse Put (string resource, List<Parameter> param, object body)
		{
			bool containsAT = false;

			var request = new RestRequest (resource, Method.PUT);
			List<string> names = new List<string> ();
			foreach (Parameter p in param) {
				names.Add (p.Name + "={" + p.Name + "}");
				if (p.Name.Equals ("access_token")) {
					containsAT = true;
				}
				p.Type = ParameterType.UrlSegment;
				request.AddParameter (p);
			}

			request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

			request.AddHeader ("Accept", "application/json");
			request.AddHeader ("Content-Type", "application/json");
			request.RequestFormat = DataFormat.Json;

			request.AddBody (body);

			var response = ExecuteRequest (request);

			if (!string.IsNullOrEmpty (this.RefreshToken) && response.StatusCode == HttpStatusCode.NotFound && containsAT) {
				refreshToken ();

				request = new RestRequest (resource, Method.PUT);
				names = new List<string> ();
				foreach (Parameter p in param) {
					if (p.Name.Equals ("access_token")) {
						p.Value = this.AccessToken;
					}
					names.Add (p.Name + "={" + p.Name + "}");
					p.Type = ParameterType.UrlSegment;
					request.AddParameter (p);
				}

				request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

				request.AddHeader ("Accept", "application/json");
				request.AddHeader ("Content-Type", "application/json");
				request.RequestFormat = DataFormat.Json;

				request.AddBody (body);
				response = ExecuteRequest (request);
			}

			return response;
		}

		public IRestResponse Delete (string resource, List<Parameter> param)
		{
			bool containsAT = false;

			var request = new RestRequest (resource, Method.DELETE);
			List<string> names = new List<string> ();
			foreach (Parameter p in param) {
				names.Add (p.Name + "={" + p.Name + "}");
				if (p.Name.Equals ("access_token")) {
					containsAT = true;
				}
				p.Type = ParameterType.UrlSegment;
				request.AddParameter (p);
			}

			request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

			request.AddHeader ("Accept", "application/json");

			var response = ExecuteRequest (request);

			if (!string.IsNullOrEmpty (this.RefreshToken) && response.StatusCode == HttpStatusCode.NotFound && containsAT) {
				refreshToken ();

				request = new RestRequest (resource, Method.DELETE);
				names = new List<string> ();
				foreach (Parameter p in param) {
					if (p.Name.Equals ("access_token")) {
						p.Value = this.AccessToken;
					}
					names.Add (p.Name + "={" + p.Name + "}");
					p.Type = ParameterType.UrlSegment;
					request.AddParameter (p);
				}

				request.Resource = resource + "?" + String.Join ("&", names.ToArray ());

				request.AddHeader ("Accept", "application/json");

				response = ExecuteRequest (request);
			}

			return response;
		}

		public IRestResponse ExecuteRequest(RestRequest request) {
			client.UserAgent = sdkVersion;
			return client.Execute(request);
		}


	}
}