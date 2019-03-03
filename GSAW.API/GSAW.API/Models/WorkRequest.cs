using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GSAW.API.Models
{
    public class WorkRequest
    {
        public string WorkRequestId { get; set; }
        public string ImageUrl { get; set; }
        public string WebhookUrl { get; set; }
    }
}
