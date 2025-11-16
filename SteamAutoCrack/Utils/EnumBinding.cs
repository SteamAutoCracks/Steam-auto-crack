using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace SteamAutoCrack.Utils;

public class EnumDescriptionConverter : IValueConverter
{
    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var myEnum = (Enum)value;
        var description = GetEnumDescription(myEnum);
        return description;
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Empty;
    }

    private string GetEnumDescription(Enum enumObj)
    {
        try
        {
            var fieldInfo = enumObj.GetType().GetField(enumObj.ToString());
            var attribArray = fieldInfo?.GetCustomAttributes(false);

            if (attribArray == null || attribArray.Length == 0)
            {
                return enumObj.ToString();
            }

            DescriptionAttribute? attrib = null;

            foreach (var att in attribArray)
                if (att is DescriptionAttribute)
                    attrib = att as DescriptionAttribute;

            if (attrib != null)
                return attrib.Description;

            return enumObj.ToString();
        }
        catch (Exception e)
        {
            return string.Empty;
        }
        
    }
}
