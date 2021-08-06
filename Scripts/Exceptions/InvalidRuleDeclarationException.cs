using System;

namespace LvlGen.Scripts.Exceptions
{
    public class InvalidRuleDeclarationException : Exception
    {
        public override string Message => "Duplicate tag in special rule declaration, can only define one rule per tag";
    }
}
