using System;

namespace FrizzNet.Core
{
    /// <summary>
    /// Custom attribute used to attach helpful descriptions and documentation links 
    /// directly to FrizzNet runtime components for in-editor guides.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class FrizzHelpAttribute : Attribute
    {
        public string Description { get; }
        public string DocLink { get; }

        public FrizzHelpAttribute(string description, string docLink = "")
        {
            Description = description;
            DocLink = docLink;
        }
    }
}
