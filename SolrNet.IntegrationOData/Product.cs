﻿using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using SolrNet.Attributes;
using SolrNet.Mapping;

namespace SolrNet.IntegrationOData
{
    public class Product
    {
        [DataMember]
        [SolrUniqueKey("id")]
        public string Id { get; set; }

        [DataMember]
        [SolrField("manu_exact")]
        public string Manufacturer { get; set; }

        [SolrField("cat")]
        public ICollection<string> Categories { get; set; }

        [DataMember]
        [SolrField("price")]
        public decimal Price { get; set; }

        [SolrField("sequence_i")]
        public int Sequence { get; set; }

        [SolrField("popularity")]
        public decimal? Popularity { get; set; }

        public decimal NotMapped { get; set; }

        [SolrField("inStock_b")]
        public bool InStock { get; set; }
    }
}