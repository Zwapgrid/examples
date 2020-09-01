// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
function bindEvent(element, eventName, eventHandler) {
    if (element.addEventListener) {
        element.addEventListener(eventName, eventHandler, false);
    } else if (element.attachEvent) {
        element.attachEvent('on' + eventName, eventHandler);
    }
}

function sendToIframe(type, data){
    let zsiFrame = document.getElementById('zsiFrame');
    if(!zsiFrame){
        return;
    }

    let message = {
        type: type,
        data: data
    }
    zsiFrame.contentWindow.postMessage(message, '*');
}

function callApi(method, url, callback){
    $.ajax({
        type: method,
        url: url,
        contentType: "application/json; charset=utf-8",
        dataType: "json",

    }).done(callback)
}

bindEvent(window, 'message', function (e) {
    let zsiFrame = document.getElementById('zsiFrame');
    if(!zsiFrame){
        return;
    }
    
    let validHost =  new URL(zsiFrame.attributes['src'].value).host;
    if(!(new URL(e.origin)).host.includes(validHost)){
        return;
    }
    
    let message = JSON.parse(e.data);
    if (!message) {
        return;
    }

    // These arrays will be expanded in future with new message types and handlers
    let messageTypes = ['integration.created', 'client.authenticated', 'client.accessTokenMissing'];
    let handlers = [processIntegrationCreatedResult, processClientAuthenticatedMessage, processAccessTokenMissingMessage];

    if (messageTypes.indexOf(message.type) < 0){
        return;
    }

    let handler = handlers[messageTypes.indexOf(message.type)];
    handler(message.data);
});

// Message handlers go here
function processIntegrationCreatedResult(systems) {
    if (!systems || systems.length != 2) {
        return;
    }
    let sourceSystem = systems[0];
    let targetSystem = systems[1];
    let message = document.getElementById('message');
    if (message) {
        message.removeAttribute("hidden");
        message.innerHTML = 'Integration between <b>' + sourceSystem + '</b> and <b>' + targetSystem + '</b> was created successfully!';
    }
}

function processClientAuthenticatedMessage(authMessage){
    let code = authMessage.authorizationCode;
    callApi('GET', '/Index?handler=AccessToken&authCode=' + code, function(data){
        let accToken = data.accessToken;
        let otc = data.otc;
        sendToIframe('accessToken', accToken);
    })
}

function processAccessTokenMissingMessage(){
    callApi('GET', '/Index?handler=RefreshAccessToken', function(data){
        let accToken = data.accessToken;
        sendToIframe('accessToken', accToken);
    })
}