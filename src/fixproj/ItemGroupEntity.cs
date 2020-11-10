using System.Collections.Generic;
using System.Xml.Linq;

namespace fixproj
{
    public class ItemGroupEntity
    {
        /// <summary>
        /// Represents local name.
        /// </summary>
        public string LocalName { get; set; }

        /// <summary>
        /// Represents a collection of node elements.
        /// </summary>
        public List<XElement> Element { get; set; }
    }
}
