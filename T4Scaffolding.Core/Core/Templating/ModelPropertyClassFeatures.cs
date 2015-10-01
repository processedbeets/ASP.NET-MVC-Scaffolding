using System.Collections.Generic;

namespace T4Scaffolding.Core.Templating
{
    static class ModelPropertyClassFeatures
    {
        public static IDictionary<string, string> ModelPropertySourceForLanguage = new Dictionary<string, string> {
            {
                "cs",
                @"<#+
	dynamic Model {
        get {
            dynamic @this = this;
            return @this.Host.Model;
        }
	}
#>"
            },

            {
                "vb",
                @"<#+
    Public ReadOnly Property Model As Object
        Get
            Dim thisDynamic As Object = Me
            Return thisDynamic.Host.Model
        End Get
    End Property
#>"
            }
        };
    }
}
