## Marketplace .Net example project
### Introduction
This example is based on a regular ASP.NET Core Web Application with individual authentication. It's intended to show how to implement the Marketplace in a .Net application as simple as possible.

Most of relevant code is contained in Index.cshtml.cs.

### Marketplace prerequisites
Zwapgrid is built on tenants that contain users, the using service needs some abstraction of these concepts.

For Zwapgrid to be able to connect to an external service, a connection containing credentials is needed. These connections are different depending on the system Zwapgrid should connect to.

Authentication is based on Oauth2, but has been extended to be secure even though information sometimes needs to be passed in plain text.

Zwapgrid has a public API that is used for creating connections and accounts. Swagger can be found [here](https://api.zwapgrid.com/swagger/index.html?urls.primaryName=Zwapgrid%20API%20V1).

When Marketplace is used for a specific user and company, a tenant and user will be created in Zwapgrid. The user will **NOT** receive any notification about this. Make sure the user is aware that you are passing their info to a third-party!

#### Configuration
For the Marketplace to work, a client must be created in Zwapgrid and **clientId** and **clientSecret** from that client should be used in requests. To create this client, create and login to your Zwapgrid account and navigate to Administration => Integration => Clients tab. After creating a client, copy **clientId** and **clientSecret** into appsettings.json.

#### Basic flow
When Marketplace should be embedded in a service, these steps should be taken:
1. A one-time code should be obtained by posting to API endpoint `api/v1/oauth2/one-time-code`.
2. If this is a first-time user, a connection should be created in Zwapgrid with the credentials for that user to your system by posting to API endpoint `api/v1/connections`. Save the resulting connection id for later use! This could be omitted if you want a Marketplace where the user have to enter their credentials themselves.
3. Encrypt connection id together with the one-time code by using your public key from `api/v1/me/public-key`.
4. Concatenate and embed URL for zwapstore by base URL 'https://app.zwapgrid.com/zwapstore' and adding query parameters as follows:
 - `otc` (Required): The one-time code.
 - `companyId` (Required): Identifier of the users company (Organization number in case of Swedish company).
 - `name` (Required on first use of companyId): The name of the users company. Will become the name of the company in Zwapgrid.
 - `email` (Required on first use of companyId): An email to the user. Will become contact email of the created user in Zwapgrid. The user will **NOT** receive any notification about this.
 - `tenancyName` (Optinal, but recommended on first use of companyId): Zwapgrid id of the users company. This will become the sub-domain of the Zwapgrid account, i.e. passing `zwapgrid-ab` will create a Zwapgrid account at `zwapgrid-ab.zwapgrid.com`. Subdomain formatting rules therefore apply. Recommended to use email domain if corporate or url-safe company name. If omitted will use url-safe company name.
 - `sourceConnectionId` (Optional but recommended, required if hideSource is true): The connection id. If omitted, the user will have to enter their own credentials.
 - `source` (Optional but recommended, required if hideSource is true): The system key for the source system. Generally your system key. If omitted, the user will have to select the source system.
 -  `export.connection.{parameter}` (Optional) Could be used to send the source credentials in plain text.
 - `hideSource` (Optional but recommended): If sourceConnectionId and source is set, the user don't have to make any options, so the source can be hidden. Recommended to make the Marketplace feel more integrated into your service.
 -  `output.connection.{parameter}` (Optional) Could be used to send the target credentials in plain text.
 - `target`, `targetConnectionId` and `hideTarget` (Optional): The same as for source, these can be used to set/configure specific target system as well.
 - `lang` (Optional, default is English): The UI language for this logged in user. The language should be supported in your system languages list (Zwapgrid -> Administration -> Languages -> Language Code). Examples: en, sv, de
 Example embedding code: 
 ```
<iframe src="https://app.zwapgrid.com/marketplace?otc={token}&clientId={yourClientId}&companyId=123456-1234&name=Zwapgrid AB&email=user@zwapgrid.com&sourceConnectionId={encryptedConnectionId}&source={yourSystemKey}&hideSource=True" height="600px" width="100%" style="border: 0;">
</iframe>
```
 ```
<iframe src="https://app.zwapgrid.com/marketplace?otc={token}&companyId=my-company&name=My Company&email=user@my-company.com&sourceConnectionId={encryptedConnectionId}&source={yourSystemKey}&hideSource=True" height="600px" width="100%" style="border: 0;">
</iframe>
```
**Each query parameter should be encoded, e.g.: email=user%2Bme@gmail.com should be instead of email=user+me@gmail.com.**
 
### Notes
This project is not at all adapted to code conventions and principles, we've tried keeping all needed code in the same place and uncluttered by other code. We recommend adding proper error handling and adapting develop principles.

### Issues or questions
Feel free to open an issue or PR or contact us at support@zwapgrid.com.
