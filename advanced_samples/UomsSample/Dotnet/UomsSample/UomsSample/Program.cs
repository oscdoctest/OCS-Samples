﻿// <copyright file="Program.cs" company="OSIsoft, LLC">
//
//Copyright 2019 OSIsoft, LLC
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//<http://www.apache.org/licenses/LICENSE-2.0>
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
// </copyright>

using System;
using System.Configuration;
using System.Threading.Tasks;

using OSIsoft.Data;
using OSIsoft.Data.Reflection;
using System.Collections.Generic;
using OSIsoft.Identity;

namespace UomsSample
{
    class Program
    {
        private static readonly string ResourcePrefix = ConfigurationManager.AppSettings["ResourcePrefix"];

        private static readonly string TypeId = $"{ResourcePrefix} Uom";

        private static readonly string StreamWithPropertyOverriden = $"{ResourcePrefix} UomPropertyOverridden";

        private static readonly string StreamWithoutPropertyOverriden = $"{ResourcePrefix} UomNoPropertyOverridden";

        private static Random Random = new Random();

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        public static async Task MainAsync()
        {
            string tenantId = ConfigurationManager.AppSettings["Tenant"];
            string namespaceId = ConfigurationManager.AppSettings["Namespace"];
            string resource = ConfigurationManager.AppSettings["Resource"];
            string clientId = ConfigurationManager.AppSettings["ClientId"];
            string clientSecret = ConfigurationManager.AppSettings["ClientSecret"];

            AuthenticationHandler authenticationHandler = new AuthenticationHandler(resource, clientId, clientSecret);
            SdsService service = new SdsService(new Uri(resource), authenticationHandler);

            ISdsMetadataService MetadataService = service.GetMetadataService(tenantId, namespaceId);
            ISdsDataService DataService = service.GetDataService(tenantId, namespaceId);

            /*
             * The following code provides an implementation for getting all the SdsUom ID's for each quantity.
             * If you are not aware with which SdsUom ID to use, you can uncomment the below code and find out the
             * uom id.
             * 
             * e.g. I am using degree_fahrenheit and  degree_celsius UOMS for temperature SdsQuantity.
             * 
             * 
            IEnumerable<SdsUomQuantity> sdsUomQuantities = await MetadataService.GetQuantitiesAsync();
            IEnumerable<SdsUom> sdsUoms = null;

            foreach (SdsUomQuantity sdsUomQuantity in sdsUomQuantities)
            {
                sdsUoms = await MetadataService.GetQuantityUomsAsync(sdsUomQuantity.Id);

                foreach (SdsUom sdsUom in sdsUoms)
                {
                    Console.WriteLine(sdsUom.Id);
                }
            }
            */

            #region stream and type creation

            // Creating a Sdstype
            SdsType sdsType = SdsTypeBuilder.CreateSdsType<Widget>();
            sdsType.Id = TypeId;

            sdsType = await MetadataService.GetOrCreateTypeAsync(sdsType);

            //Creating a Stream overriding the distance property.
            SdsStream sdsStreamOne = new SdsStream()
            {
                Id = StreamWithPropertyOverriden,
                TypeId = TypeId,
                Name = "UomStreamSourceWithPropertyOverridden",
                PropertyOverrides = new List<SdsStreamPropertyOverride>()
            };

            //Overriding the UOM of the distance property to be kilometer instead of mile.
            sdsStreamOne.PropertyOverrides.Add(new SdsStreamPropertyOverride()
            {
                Uom = "kilometer",
                SdsTypePropertyId = "Distance"
            });

            sdsStreamOne = await MetadataService.GetOrCreateStreamAsync(sdsStreamOne);

            //Creating a Stream without overriding properties.
            SdsStream sdsStreamTwo = new SdsStream()
            {
                Id = StreamWithoutPropertyOverriden,
                TypeId = TypeId,
                Name = "UomStreamSourceWithNoPropertyOverridden",
            };

            sdsStreamTwo = await MetadataService.GetOrCreateStreamAsync(sdsStreamTwo);

            // Generating data
            IList<Widget> data = new List<Widget>();
            for (int i = 0; i < 10; i++)
            {
                Widget widget = new Widget();
                widget.Time = DateTime.UtcNow.AddSeconds(i);
                widget.Temperature = Random.Next(1, 100);
                widget.Distance = Random.Next(1, 100);
                data.Add(widget);
            }

            /* In stream one, the temperature value will be inserted as Fahrenheit since we have defined the 
             * default uom as Fahrenheit for Temperature in the Widget class. The distance value will be 
             * inserted as kilometer, as we have overridden the Distance property in stream one, 
             * regardless of the default uom for Distance in the Widget class.
             */
            await DataService.InsertValuesAsync<Widget>(sdsStreamOne.Id, data);

            /* In stream two, the temperature value will be inserted as Fahrenheit and the distance will be inserted as mile.
             *
             */
            await DataService.InsertValuesAsync<Widget>(sdsStreamTwo.Id, data);

            /*
             * The last value stored in stream one. 
             */
            Widget widgetFromStreamOne = await DataService.GetLastValueAsync<Widget>(sdsStreamOne.Id);

            Console.WriteLine($"In stream one, the distance is {widgetFromStreamOne.Distance} kilometers and the temperature is {widgetFromStreamOne.Temperature} degrees fahrenheit");
            Console.WriteLine();
            /*
             * The last value stored in stream two. 
             */
            Widget widgetFromStreamTwo = await DataService.GetLastValueAsync<Widget>(sdsStreamTwo.Id);

            Console.WriteLine($"In stream two, the distance is {widgetFromStreamTwo.Distance} miles and the temperature is {widgetFromStreamTwo.Temperature} degrees fahrenheit");
            Console.WriteLine();

            /*
             * If you want your data to be in specified uom, you can override your properties while making a call.
             * In the following, I want the temperature to be in Celsius, and the distance to be in feet.
             * 
             * Then you can pass IList<SdsStreamPropertyOverride> to DataService while getting values.
             * 
             */
            IList<SdsStreamPropertyOverride> requestOverrides = new List<SdsStreamPropertyOverride>();

            requestOverrides.Add(new SdsStreamPropertyOverride()
            {
                Uom = "degree_celsius",
                SdsTypePropertyId = "Temperature"
            });

            requestOverrides.Add(new SdsStreamPropertyOverride()
            {
                Uom = "foot",
                SdsTypePropertyId = "Distance"
            });

            /*
             * In the following call, data will be converted from Fahrenheit to Celsius for the temperature property,
             * and from kilometer to foot for the distance property.
             * 
             * Uoms in Stream one (Temperature : Fahrenheit, Distance : Kilometer)
             * 
             */
            widgetFromStreamOne = await DataService.GetLastValueAsync<Widget>(sdsStreamOne.Id, requestOverrides);

            Console.WriteLine($"In stream one, the distance is {widgetFromStreamOne.Distance} foot and the temperature is {widgetFromStreamOne.Temperature} degrees celsius");
            Console.WriteLine();

            /*
             * In the following call, data will be converted from Fahrenheit to Celsius for the temperature property, 
             * and from mile to foot for the distance property.
             * 
             * Uoms in Stream two (Temperature : Fahrenheit, Distance : Mile) 
             * 
             */
            widgetFromStreamTwo = await DataService.GetLastValueAsync<Widget>(sdsStreamTwo.Id, requestOverrides);

            Console.WriteLine($"In stream two, the distance is {widgetFromStreamTwo.Distance} foot and the temperature is {widgetFromStreamTwo.Temperature} degrees celsius");
            Console.WriteLine();

            #endregion stream and type creation

            #region Deletion of Streams and type

            await MetadataService.DeleteStreamAsync(sdsStreamOne.Id);
            await MetadataService.DeleteStreamAsync(sdsStreamTwo.Id);
            await MetadataService.DeleteTypeAsync(TypeId);

            #endregion Deletion of Streams and type
        }
    }
}
