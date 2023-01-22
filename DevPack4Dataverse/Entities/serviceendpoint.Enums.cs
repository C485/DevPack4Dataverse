/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace DevPack4Dataverse.Entities;

internal partial class ServiceEndpoint
{
    public class Enums
    {
        public enum AuthType
        {
            ACS = 1,
            SASKey = 2,
            SASToken = 3,
            WebhookKey = 4,
            HttpHeader = 5,
            HttpQueryString = 6,
            ConnectionString = 7,
            AccessKey = 8,
        }

        public enum ComponentState
        {
            Published = 0,
            Unpublished = 1,
            Deleted = 2,
            DeletedUnpublished = 3,
        }

        public enum ConnectionMode
        {
            Normal = 1,
            Federated = 2,
        }

        public enum Contract
        {
            OneWay = 1,
            Queue = 2,
            Rest = 3,
            TwoWay = 4,
            Topic = 5,
            QueuePersistent = 6,
            EventHub = 7,
            Webhook = 8,
            EventGrid = 9,
        }

        public enum IsAuthValueSet
        {
            Yes = 1,
            No = 0,
        }

        public enum IsManaged
        {
            Managed = 1,
            Unmanaged = 0,
        }

        public enum IsSASKeySet
        {
            Yes = 1,
            No = 0,
        }

        public enum IsSASTokenSet
        {
            Yes = 1,
            No = 0,
        }

        public enum MessageCharset
        {
            Default = 0,
            UTF8 = 1,
        }

        public enum MessageFormat
        {
            BinaryXML = 1,
            Json = 2,
            TextXML = 3,
        }

        public enum NamespaceFormat
        {
            NamespaceName = 1,
            NamespaceAddress = 2,
        }

        public enum SchemaType
        {
            EventGrid = 1,
            CloudEvents = 2,
        }

        public enum UseKeyVaultConfiguration
        {
            Yes = 1,
            No = 0,
        }

        public enum UserClaim
        {
            None = 1,
            UserId = 2,
            UserInfo = 3,
        }
    }
}
