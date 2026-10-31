Document Processing Service

#Overview

Document Processing Service is a .NET-based application designed to handle document uploads and asynchronous processing. 
It consists of two independent components: an ASP.NET Core Web API responsible for handling incoming requests and a .NET Worker Service responsible for background document processing.
The solution is designed to support scalability, maintainability, and separation of concerns, with plans to integrate Azure Blob Storage for document storage and Azure Service Bus for asynchronous communication between components.