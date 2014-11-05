﻿using System;
using System.Text;
using ElasticsearchCRUD;
using ElasticsearchCRUD.ContextSearch;
using LiveReindexInElasticsearch.SQLDomainModel;

namespace LiveReindexInElasticsearch.Reindex
{
	public class ReindexPersonV1ToPersonV2
	{
		private readonly ElasticsearchContext _context;
		private int indexerSize = 500;
		public ReindexPersonV1ToPersonV2()
		{
			IElasticsearchMappingResolver elasticsearchMappingResolver = new ElasticsearchMappingResolver();		
			elasticsearchMappingResolver.AddElasticSearchMappingForEntityType(typeof(PersonV2), new PersonV2IndexTypeMapping());
			elasticsearchMappingResolver.AddElasticSearchMappingForEntityType(typeof(Person), new PersonV1IndexTypeMapping());

			_context = new ElasticsearchContext("http://localhost.fiddler:9200/", elasticsearchMappingResolver);
		}

		public void SwitchAliasfromPersonV1IndexToPersonV2Index()
		{
			_context.AliasReplaceIndex("persons", "persons_v1", "persons_v2");
		}

		public void Reindex(DateTime beginDateTime)
		{
			var result = _context.SearchCreateScanAndScroll<Person>(BuildSearchModifiedDateTimeLessThan(beginDateTime),
				new ScanAndScrollConfiguration(1, TimeUnits.Minute, 100));

			var scrollId = result.PayloadResult;
			Console.WriteLine("Total Hits in scan: {0}", result.TotalHits);

			int indexPointer = 0;
			while (result.TotalHits > indexPointer - indexerSize)
			{
				Console.WriteLine("creating new documents, indexPointer: {0} Hits: {1}", indexPointer, result.TotalHits);

				var resultCollection = _context.Search<Person>(BuildSearchFromTooForScanScroll(indexPointer, indexerSize),
					scrollId);

				foreach (var item in resultCollection.PayloadResult)
				{
					_context.AddUpdateDocument(CreatePersonV2FromPerson(item), item.BusinessEntityID);
				}
				_context.SaveChanges();
				indexPointer = indexPointer + indexerSize;
			}
		}

		public void ReindexUpdateChangesWhileReindexing(DateTime beginDateTime)
		{
			var result = _context.SearchCreateScanAndScroll<Person>(BuildSearchModifiedDateTimeGreaterThan(beginDateTime),
				new ScanAndScrollConfiguration(1, TimeUnits.Minute, indexerSize));

			var scrollId = result.PayloadResult;
			Console.WriteLine("Total Hits in scan: {0}", result.TotalHits);

			int indexPointer = 0;
			while (result.TotalHits > indexPointer - 100)
			{
				Console.WriteLine("creating new documents, indexPointer: {0} Hits: {1}", indexPointer, result.TotalHits);

				var resultCollection = _context.Search<Person>(BuildSearchFromTooForScanScroll(indexPointer, indexerSize),
					scrollId);

				foreach (var item in resultCollection.PayloadResult)
				{
					_context.AddUpdateDocument(CreatePersonV2FromPerson(item), item.BusinessEntityID);
				}
				_context.SaveChanges();
				indexPointer = indexPointer + indexerSize;
			}
		}

		private PersonV2 CreatePersonV2FromPerson(Person item)
		{
			return new PersonV2
			{
				BusinessEntityID = item.BusinessEntityID,
				PersonType = item.PersonType,
				NameStyle = item.NameStyle,
				Title = item.Title,
				FirstName = item.FirstName,
				MiddleName = item.MiddleName,
				LastName = item.LastName,
				Suffix = item.Suffix,
				EmailPromotion = item.EmailPromotion,
				AdditionalContactInfo = item.AdditionalContactInfo,
				Demographics = item.Demographics,
				rowguid = item.rowguid,
				ModifiedDate = item.ModifiedDate,
				Deleted = false
			};
		}

		//{
		//	"query" : {
		//		"match_all" : {}
		//	}
		//}
		private string BuildSearchMatchAll()
		{
			var buildJson = new StringBuilder();
			buildJson.AppendLine("{");
			buildJson.AppendLine("\"query\": {");
			buildJson.AppendLine("\"match_all\" : {}");
			buildJson.AppendLine("}");
			buildJson.AppendLine("}");

			return buildJson.ToString();
		}

		//{   
		//   "from" : 100 , "size" : 100
		//}
		private string BuildSearchFromTooForScanScroll(int from, int size)
		{
			var buildJson = new StringBuilder();
			buildJson.AppendLine("{");
			buildJson.AppendLine("\"from\" : " + from + ", \"size\" : " + size);
			buildJson.AppendLine("}");

			return buildJson.ToString();
		}

		private string BuildSearchModifiedDateTimeLessThan(DateTime dateTimeUtc)
		{
			return BuildSearchRange("lt", "modifieddate", dateTimeUtc);
		}

		private string BuildSearchModifiedDateTimeGreaterThan(DateTime dateTimeUtc)
		{
			return BuildSearchRange("gte", "modifieddate", dateTimeUtc);
		}

		//{
		//   "query" :  {
		//	   "range": {  "modifieddate": { "lt":   "2003-12-29T00:00:00"  } }
		//	}
		//}
		private string BuildSearchRange(string lessThanOrGreaterThan, string updatePropertyName, DateTime dateTimeUtc)
		{
			string isoDateTime = dateTimeUtc.ToString("s");
			var buildJson = new StringBuilder();
			buildJson.AppendLine("{");
			buildJson.AppendLine("\"query\": {");
			buildJson.AppendLine("\"range\": {  \"" + updatePropertyName + "\": { \"" + lessThanOrGreaterThan + "\":   \"" + isoDateTime + "\"  } }");
			buildJson.AppendLine("}");
			buildJson.AppendLine("}");

			return buildJson.ToString();
		}
	}
}


