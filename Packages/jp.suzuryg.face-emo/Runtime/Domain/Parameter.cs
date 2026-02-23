using System;
using UnityEngine;
using Object = System.Object;

namespace Suzuryg.FaceEmo.Domain
{
    public class Parameter : IEquatable<Parameter>
    {
        public string ParameterName { get; }
        public ParameterType ParameterType { get; }
        public float Value { get; }

        public Parameter(string parameterName, ParameterType parameterType, float value)
        {
            ParameterName = parameterName;
            ParameterType = parameterType;
            Value = value;
        }

        bool IEquatable<Parameter>.Equals(Parameter other)
        {
            if (other is null) {
                return false;
            }
            
            return ParameterName == other.ParameterName && ParameterType == other.ParameterType && Mathf.Approximately(this.Value, other.Value);
        }
        
        public override bool Equals(Object obj)
        {
            if (obj is Parameter other)
            {
                return ParameterName == other.ParameterName && ParameterType == other.ParameterType && Mathf.Approximately(this.Value, other.Value);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return new { ParameterName, ParameterType, Value }.GetHashCode();
        }

        public static bool operator == (Parameter param1, Parameter param2)
        {
            if (param1 is null || param2 is null) 
            {
                return ReferenceEquals(param1, param2);
            }
            
            return param1.Equals(param2);
        }

        public static bool operator !=(Parameter param1, Parameter param2)
        {
            if (param1 is null || param2 is null) 
            {
                return !ReferenceEquals(param1, param2);
            }
            
            return !param1.Equals(param2);
        }
    }
}