using Suzuryg.FaceEmo.Domain;
using UnityEngine;

namespace Suzuryg.FaceEmo.Components.Data
{
    public class SerializableParameter : ScriptableObject {
        public string ParameterName;
        public ParameterType ParameterType;
        public float Value;

        public void Save(Parameter parameter)
        {
            ParameterName = parameter.ParameterName;
            ParameterType = parameter.ParameterType;
            Value = parameter.Value;
        }

        public Parameter Load()
        {
            return new Parameter(ParameterName, ParameterType, Value);
        }
        
    }
}