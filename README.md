# normaliantranslation
This is sample web application and Azure Function app for AI Translation

## How to run
You have to create Azure resources as follows.
- App Service: Setup Managed Identity, then assign KeyVault RBAC to access Secrets
- Azure Function : Setup Managed Identity, then assign KeyVault RBAC to access Secrets and Azure Storage RBAC to access Blob Storage
- Azure Storage : create "source" and "translated" containers
- Azure KeyVault : create ACS-CONNECTIONSTRING, AITRANSLATOR-ENDPOINT, AITRANSLATOR-SUBSCRIPTIONID, SENDER-ADDRESS, STORAGE-CONNECTIONSTRING secrets
- AI Translator Cognitive Service 
- Azure Communication Service: Setup email sender

