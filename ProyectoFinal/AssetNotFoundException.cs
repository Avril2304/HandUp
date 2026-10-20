using System;

namespace ProyectoFinal
{
    public class AssetNotFoundException : HandUpException
    {
        public AssetNotFoundException() { }
        
        public AssetNotFoundException(string message) : base(message) { }
        
        public AssetNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}
