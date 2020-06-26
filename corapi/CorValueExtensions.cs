using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;

namespace corapi
{
    public static class CorValueExtensions
    {
        public static T GetValue<T>(this CorValue value) => (T) GetValue(value);

        private static object GetValue(this CorValue value)
        {
            var rv = value.CastToReferenceValue();
            if (rv != null)
            {
                if (rv.IsNull)
                {
                    value._typename = rv.ExactType.Type;
                    return null;
                }

                return GetValue(rv.Dereference());
            }

            var bv = value.CastToBoxValue();
            if (bv != null)
                return GetValue(bv.GetObject());

/*_type_map = { 'System.Boolean': ELEMENT_TYPE_BOOLEAN,    
  'System.SByte'  : ELEMENT_TYPE_I1, 'System.Byte'   : ELEMENT_TYPE_U1,    
  'System.Int16'  : ELEMENT_TYPE_I2, 'System.UInt16' : ELEMENT_TYPE_U2,    
  'System.Int32'  : ELEMENT_TYPE_I4, 'System.UInt32' : ELEMENT_TYPE_U4,    
  'System.IntPtr' : ELEMENT_TYPE_I,  'System.UIntPtr': ELEMENT_TYPE_U,   
  'System.Int64'  : ELEMENT_TYPE_I8, 'System.UInt64' : ELEMENT_TYPE_U8,    
  'System.Single' : ELEMENT_TYPE_R4, 'System.Double' : ELEMENT_TYPE_R8,    
  'System.Char'   : ELEMENT_TYPE_CHAR, }*/

            var typeMap = new List<KeyValuePair<CorElementType, string>>();
            typeMap.AddRange(new[]
            {
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_BOOLEAN, "System.Boolean"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_I1, "System.SByte"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_U1, "System.Byte"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_I2, "System.Int16"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_U2, "System.UInt16"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_I4, "System.Int32"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_U4, "System.UInt32"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_I, "System.IntPtr"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_U, "System.UIntPtr"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_I8, "System.Int64"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_U8, "System.UInt64"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_R4, "System.Single"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_R8, "System.Double"),
                new KeyValuePair<CorElementType, string>(CorElementType.ELEMENT_TYPE_CHAR, "System.Char")
            });

            if (typeMap.Exists(t => t.Key.Equals(value.Type)))
                return value.CastToGenericValue().GetValue();
            else if (value.Type == CorElementType.ELEMENT_TYPE_STRING)
                return value.CastToStringValue().String;
            else if (value.Type == CorElementType.ELEMENT_TYPE_VALUETYPE)
            {
                var typeValue = value.ExactType.Type;
                if (typeMap.Exists(t => t.Value.Equals(value._typename)))
                {
                    var gv = value.CastToGenericValue();
                    return gv.UnsafeGetValueAsType(typeMap.Find(t => t.Value.Equals(value._typename)).Key);
                }
                else
                    return value.CastToObjectValue();
            }
            else if (
                new[] {CorElementType.ELEMENT_TYPE_CLASS, CorElementType.ELEMENT_TYPE_OBJECT}.Contains(value.Type))
                return new object(); //value.CastToObjectValue();
            else
                return "Unknown";
        }
    }
}