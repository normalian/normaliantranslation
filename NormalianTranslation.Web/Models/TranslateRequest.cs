using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NormalianTranslation.Web.Models
{
    public class TranslateRequest
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "contentname")]
        public string ContentName { get; set; }

        [JsonProperty(PropertyName = "emailaddress")]
        public string EmailAddress { get; set; }

        [JsonProperty(PropertyName = "sourceuri")]
        public string SourceURI { get; set; }

        [JsonProperty(PropertyName = "translateduri")]
        public string TranslatedURI { get; set; }

        [JsonProperty(PropertyName = "status")]
        public TranslateRequestStatus Status { get; set; }

        [JsonProperty(PropertyName = "CreatedDate")]
        public DateTime CreatedDate { get; set; }
    }
}

public enum TranslateRequestStatus
{
    InProgress,
    Error,
    Successed
}
