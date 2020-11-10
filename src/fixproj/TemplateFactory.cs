using System;
using System.Xml.Linq;
using fixproj.Abstract;
using fixproj.Implementation;

namespace fixproj
{
    internal class TemplateFactory
    {
        private readonly string _projectTypeValue = "Microsoft.NET.Sdk";

        /// <summary>
        /// Creates instance of IOperateOnProjectFiles implementation.
        /// </summary>
        /// <param name="file">The path of the processed file.</param>
        /// <returns></returns>
        internal IOperateOnProjectFiles Build(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentNullException(nameof(file));
            }

            var document = XDocument.Load(file);

            if (document?.Root == null)
            {
                throw new InvalidOperationException("Document is not valid");
            }

            var projectType = document.Root.Attribute("Sdk")?.Value;

            if (!string.IsNullOrWhiteSpace(projectType) && projectType.Equals(_projectTypeValue))
            {
                return new DotNetSdkTemplate(file);
            }

            return new DotNetFrameworkTemplate(file);
        }
    }
}
