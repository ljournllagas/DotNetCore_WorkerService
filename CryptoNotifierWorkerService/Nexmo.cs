using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoNotifierWorkerService
{
    public class Nexmo
    {
        public Nexmo(string from, string text, string to, string api_key, string api_secret)
        {
            this.from = from;
            this.text = text;
            this.to = to;
            this.api_key = api_key;
            this.api_secret = api_secret;
        }

        public string from { get; set; }
        public string text { get; set; }
        public string to { get; set; }
        public string api_key { get; set; }
        public string api_secret { get; set; }
    }
}
