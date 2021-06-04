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

//send a message to iframe
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
    
    //checking if message came from iframe with a valid host
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
    let handlers = [onIntegrationCreatedEvent, onClientAuthenticatedEvent, onAccessTokenMissingEvent];

    if (messageTypes.indexOf(message.type) < 0){
        return;
    }

    let handler = handlers[messageTypes.indexOf(message.type)];
    handler(message.data);
});

// Message handlers go here

// Processing integration created message
// currently it simply shows a box with system names 
function onIntegrationCreatedEvent(systems) {
    if (!systems || systems.length !== 2) {
        return;
    }
    let system1 = systems[0];
    let system2 = systems[1];
    let message = document.getElementById('message');
    if (message) {
        message.removeAttribute("hidden");
        message.innerHTML = 'Integration between <b>' + system1 + '</b> and <b>' + system2 + '</b> was created successfully!';
    }
}

// processing client authenticated message
// Client authenticated, so authorizationCode is passed. This can be exchanged
// for access and refresh tokens.
function onClientAuthenticatedEvent(authMessage){
    let {authorizationCode} = authMessage;
    callApi('GET', '/Index?handler=ClientAuthenticated&authorizationCode=' + authorizationCode, function(data){
        sendToIframe('accessToken', data.accessToken);
    })
}

// processing access token message
// getting access token, using active user's refresh token
function onAccessTokenMissingEvent(){
    callApi('GET', '/Index?handler=RefreshAccessToken', function(data){
        let accessToken = data.accessToken;
        sendToIframe('accessToken', accessToken);
    })
}