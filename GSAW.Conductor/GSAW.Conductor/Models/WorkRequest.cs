using System;
using System.Collections.Generic;
using System.Text;

namespace GSAW.Conductor.Models
{
    public class WorkRequest
    {
        public string WorkRequestId { get; set; }
        public string ImageUrl { get; set; }
        public string WebhookUrl { get; set; }
        public string CallbackUrl { get; set; }
        public object Result { get; set; }
    }
}
