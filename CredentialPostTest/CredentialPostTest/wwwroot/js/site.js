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

bindEvent(window, 'message', function (e) {
    let message = JSON.parse(e.data);
    if (!message) {
        return;
    }

    // These arrays will be expanded in future with new message types and handlers
    var messageTypes = ['integration.created'];
    var handlers = [processIntegrationCreatedResult];

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
    var message = document.getElementById('message');
    if (message) {
        message.removeAttribute("hidden");
        message.innerHTML = 'Integration between <b>' + sourceSystem + '</b> and <b>' + targetSystem + '</b> was created successfully!';
    }
}