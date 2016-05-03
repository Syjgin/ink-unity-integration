﻿using System.ComponentModel;
using System.Collections.Generic;

namespace Ink.Runtime
{
    // Order is significant for type coersion.
    // If types aren't directly compatible for an operation,
    // they're coerced to the same type, downward.
    // Higher value types "infect" an operation.
    // (This may not be the most sensible thing to do, but it's worked so far!)
    internal enum ValueType
    {
        // Used in coersion
        Int,
        Float,
        String,

        // Not used for coersion described above
        DivertTarget,
        VariablePointer
    }

    internal abstract class Value : Runtime.Object
    {
        public abstract ValueType valueType { get; }
        public abstract bool isTruthy { get; }

        public abstract Value Cast(ValueType newType);

        public abstract object valueObject { get; }

        public static Value Create(object val)
        {
            // Implicitly lose precision from any doubles we get passed in
            if (val is double) {
                double doub = (double)val;
                val = (float)doub;
            }

            // Implicitly convert bools into ints
            if (val is bool) {
                bool b = (bool)val;
                val = (int)(b ? 1 : 0);
            }

            if (val is int) {
                return new IntValue ((int)val);
            } else if (val is long) {
                return new IntValue ((int)(long)val);
            } else if (val is float) {
                return new FloatValue ((float)val);
            } else if (val is double) {
                return new FloatValue ((float)(double)val);
            } else if (val is string) {
                return new StringValue ((string)val);
            } else if (val is Path) {
                return new DivertTargetValue ((Path)val);
            }

            return null;
        }

        public override Object Copy()
        {
            return Create (valueObject);
        }
    }

    internal abstract class Value<T> : Value
    {
        public T value { get; set; }

        public override object valueObject {
            get {
                return (object)value;
            }
        }

        public Value (T val)
        {
            value = val;
        }

        public override string ToString ()
        {
            return value.ToString();
        }
    }

    internal class IntValue : Value<int>
    {
        public override ValueType valueType { get { return ValueType.Int; } }
        public override bool isTruthy { get { return value != 0; } }

        public IntValue(int intVal) : base(intVal)
        {
        }

        public IntValue() : this(0) {}

        public override Value Cast(ValueType newType)
        {
            if (newType == valueType) {
                return this;
            }

            if (newType == ValueType.Float) {
                return new FloatValue ((float)this.value);
            }

            if (newType == ValueType.String) {
                return new StringValue("" + this.value);
            }

            throw new System.Exception ("Unexpected type cast of Value to new ValueType");
        }
    }

    internal class FloatValue : Value<float>
    {
        public override ValueType valueType { get { return ValueType.Float; } }
        public override bool isTruthy { get { return value != 0.0f; } }

        public FloatValue(float val) : base(val)
        {
        }

        public FloatValue() : this(0.0f) {}

        public override Value Cast(ValueType newType)
        {
            if (newType == valueType) {
                return this;
            }

            if (newType == ValueType.Int) {
                return new IntValue ((int)this.value);
            }

            if (newType == ValueType.String) {
                return new StringValue("" + this.value);
            }

            throw new System.Exception ("Unexpected type cast of Value to new ValueType");
        }
    }

    internal class StringValue : Value<string>
    {
        public override ValueType valueType { get { return ValueType.String; } }
        public override bool isTruthy { get { return value.Length > 0; } }

        public bool isNewline { get; private set; }
        public bool isInlineWhitespace { get; private set; }
        public bool isNonWhitespace {
            get {
                return !isNewline && !isInlineWhitespace;
            }
        }

        public StringValue(string str) : base(str)
        {
            // Classify whitespace status
            isNewline = value == "\n";
            isInlineWhitespace = true;
            foreach (var c in value) {
                if (c != ' ' && c != '\t') {
                    isInlineWhitespace = false;
                    break;
                }
            }
        }

        public StringValue() : this("") {}

        public override Value Cast(ValueType newType)
        {
            if (newType == valueType) {
                return this;
            }

            if (newType == ValueType.Int) {

                int parsedInt;
                if (int.TryParse (value, out parsedInt)) {
                    return new IntValue (parsedInt);
                } else {
                    return null;
                }
            }

            if (newType == ValueType.Float) {
                float parsedFloat;
                if (float.TryParse (value, out parsedFloat)) {
                    return new FloatValue (parsedFloat);
                } else {
                    return null;
                }
            }

            throw new System.Exception ("Unexpected type cast of Value to new ValueType");
        }
    }

    internal class DivertTargetValue : Value<Path>
    {
        public Path targetPath { get { return this.value; } set { this.value = value; } }
        public override ValueType valueType { get { return ValueType.DivertTarget; } }
        public override bool isTruthy { get { throw new System.Exception("Shouldn't be checking the truthiness of a divert target"); } }
            
        public DivertTargetValue(Path targetPath) : base(targetPath)
        {
        }

        public DivertTargetValue() : base(null)
        {}

        public override Value Cast(ValueType newType)
        {
            if (newType == valueType)
                return this;
            
            throw new System.Exception ("Unexpected type cast of Value to new ValueType");
        }

        public override string ToString ()
        {
            return "DivertTargetValue(" + targetPath + ")";
        }
    }

    // TODO: Think: Erm, I get that this contains a string, but should
    // we really derive from Value<string>? That seems a bit misleading to me.
    internal class VariablePointerValue : Value<string>
    {
        public string variableName { get { return this.value; } set { this.value = value; } }
        public override ValueType valueType { get { return ValueType.VariablePointer; } }
        public override bool isTruthy { get { throw new System.Exception("Shouldn't be checking the truthiness of a variable pointer"); } }

        // Where the variable is located
        // -1 = default, unknown, yet to be determined
        // 0  = in global scope
        // 1+ = callstack element index
        public int contextIndex { get; set; }

        public VariablePointerValue(string variableName, int contextIndex = -1) : base(variableName)
        {
            this.contextIndex = contextIndex;
        }

        public VariablePointerValue() : this(null)
        {
        }

        public override Value Cast(ValueType newType)
        {
            if (newType == valueType)
                return this;

            throw new System.Exception ("Unexpected type cast of Value to new ValueType");
        }

        public override string ToString ()
        {
            return "VariablePointerValue(" + variableName + ")";
        }

        public override Object Copy()
        {
            return new VariablePointerValue (variableName, contextIndex);
        }
    }
        
}
