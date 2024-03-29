﻿using System.Collections.Generic;
using System.Xml.Linq;

namespace FixProjects
{
    public class ItemGroupEntity
    {
        /// <summary>
        ///     Gets or sets the collection of node elements.
        /// </summary>
        public List<XElement> Element { get; set; }

        /// <summary>
        ///     Gets or sets the local name.
        /// </summary>
        public string LocalName { get; set; }

        /// <summary>
        ///     Gets or sets the local attibute name.
        /// </summary>
        public string LocalAttributeName { get; set; }

        /// <summary>
        ///     Gets or sets the attribute value.
        /// </summary>
        public string LocalAttributeValue { get; set; }
    }
}