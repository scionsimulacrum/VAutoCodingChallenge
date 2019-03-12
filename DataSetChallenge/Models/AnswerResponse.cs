using System;
using System.Collections.Generic;
using System.Text;

namespace DataSetChallenge.Models
{
    public class AnswerResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int totalMilliseconds { get; set; }
    }
}
