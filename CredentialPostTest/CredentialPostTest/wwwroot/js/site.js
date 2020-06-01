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

//here goes the code which should be run on iframe parent when integration is created 
function processIntegrationCreatedResult(result) {
    let systems = result.split('|');
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

bindEvent(window, 'message', function (e) {
    processIntegrationCreatedResult(e.data);
});