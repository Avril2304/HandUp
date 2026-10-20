using System;

namespace ProyectoFinal
{
    public class InvalidInputException : HandUpException
    {
        public InvalidInputException(string message) : base(message)
        {
        }
    }
}
