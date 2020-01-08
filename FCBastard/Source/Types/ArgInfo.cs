using System;
using System.Collections.Generic;

public struct ArgInfo
{
    public readonly string Name;
    public readonly string Value;

    public bool HasName
    {
        get { return !String.IsNullOrEmpty(Name); }
    }

    public bool HasValue
    {
        get { return !String.IsNullOrEmpty(Value); }
    }

    public bool IsEmpty
    {
        get { return !HasName && !HasValue; }
    }

    public bool IsSwitch
    {
        get { return HasName && !HasValue; }
    }

    public bool IsVariable
    {
        get { return HasName && HasValue; }
    }
    
    public bool IsValue
    {
        get { return HasValue && !HasName; }
    }

    public override string ToString()
    {
        if (HasName)
        {
            return (HasValue)
                ? $"[Arg({Name}) : '{Value}']"
                : $"[Arg({Name})]";
        }

        return (HasValue)
            ? $"[Arg : '{Value}']"
            : String.Empty;
    }

    public static implicit operator ArgInfo(string s)
    {
        return new ArgInfo(s);
    }
    
    public ArgInfo(string arg)
    {
        if (arg == null)
            throw new ArgumentNullException("Argument cannot be null.", nameof(arg));

        var _arg = arg.TrimStart('-');

        if (_arg != arg)
        {
            if (_arg.Length > 0)
            {
                var splitIdx = _arg.IndexOf(':');

                if (splitIdx != -1)
                {
                    // set variable to value
                    Name = _arg.Substring(0, splitIdx).ToLower();
                    Value = _arg.Substring(splitIdx + 1);
                }
                else
                {
                    // option toggle
                    Name = _arg.ToLower();
                    Value = String.Empty;
                }
            }
            else
            {
                // empty option (e.g. '--')
                Name = String.Empty;
                Value = String.Empty;
            }
        }
        else
        {
            // explicit argument
            Name = String.Empty;
            Value = arg;
        }
    }

    public ArgInfo(string name, string value)
    {
        Name = name;
        Value = value;
    }
}