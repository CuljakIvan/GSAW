using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GSAW.API.Models
{
    public class GenericMultipartFormModel
    {
        public IFormFile Source { get; set; }
        public string Webhook { get; set; }

        public GenericMultipartFormModel()
        {
            Webhook = string.Empty;
        }
    }
}
