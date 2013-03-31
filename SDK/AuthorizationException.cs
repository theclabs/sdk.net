using System;

namespace MercadoLibre.SDK
{
	public class AuthorizationException : Exception
	{
		public AuthorizationException ()
		{
		}

		public AuthorizationException(string msg, Exception ex) : base(msg, ex) 
        {
		}

        public AuthorizationException(int webStatusCode)
        {
           // manejo de los webStatus Code aqui llega el codigo numerico
            // Mas info en http://msdn.microsoft.com/es-AR/library/system.net.httpstatuscode(v=vs.80).aspx

        }
	}
}

