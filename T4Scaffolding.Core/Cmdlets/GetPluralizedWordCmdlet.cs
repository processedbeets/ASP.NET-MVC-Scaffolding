using System;
using System.Data.Entity.Design.PluralizationServices;
using System.Globalization;
using System.Management.Automation;
using System.Threading;

namespace T4Scaffolding.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "PluralizedWord")]
    public class GetPluralizedWordCmdlet : ScaffoldingBaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
        public string Word { get; set; }

        [Parameter]
        public string Culture { get; set; }

        public GetPluralizedWordCmdlet() : base(null, null, null)
        {
            Culture = Thread.CurrentThread.CurrentUICulture.Name;
        }

        protected override void ProcessRecordCore()
        {
            PluralizationService pluralizationService;

            try {
                pluralizationService = PluralizationService.CreateService(new CultureInfo(Culture));
            } catch(NotImplementedException) {
                // Unsupported culture
                WriteDebug(string.Format("Cannot pluralize '{0}' because culture '{1}' is not yet supported by System.Data.Entity.Design.PluralizationServices. Leaving the word unpluralized.", Word, Culture));
                WriteObject(Word);
                return;
            }

            WriteObject(pluralizationService.Pluralize(Word));
        }
    }
}
