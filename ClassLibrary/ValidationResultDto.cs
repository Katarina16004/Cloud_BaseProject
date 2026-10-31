using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public class ValidationResultDto
    {
        public bool IsValid { get; set; }

        public string Message { get; set; }

        public ValidationResultDto() { }
        public ValidationResultDto(bool isValid, string message) 
        { 
            IsValid = isValid; 
            Message = message;
        }

    }
}
