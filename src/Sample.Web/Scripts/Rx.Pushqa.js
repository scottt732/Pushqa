﻿/// <reference path="rx.js" />
/// <reference path="jquery-2.0.2.js" />
(function () {
    /// <param name="$" type="jQuery" />
    "use strict";

    if (typeof ($.signalR) !== "function") {
        throw "SignalR: SignalR is not loaded. Please ensure jquery.signalR.js is referenced before Rx.Pushqa.";
    }

    if (typeof (Rx) !== "object") {
        throw "Rx: RxJs is not loaded. Please ensure rx.min.js is referenced before Rx.Pushqa.";
    }

    var Resource = function Resource(signalR, resourceName, initialFilter) {
        this.signalR = signalR;
        this.resourceName = resourceName;
        this.subject = new Rx.Subject();
        this.currentFilter = initialFilter;
        this.connected = false;
    };

    Resource.prototype = {
        updateFilter: function (filter) {
            this.currentFilter = filter;
            if (filter != null) {
                if (this.connected) {
                    this.signalR.send(this.resourceName + ';;;' + filter);
                }
            } else {
                this.clearFilter();
            }
        },
        clearFilter: function () {
            this.currentFilter = '';
            if (this.connected) {
                this.signalR.send(resourceName);
            }
        },
        onConnected: function () {
            this.connected = true;
            if (this.currentFilter != null && this.currentFilter != '') {
                this.signalR.send(this.resourceName + ';;;' + this.currentFilter);
            } else {
                this.signalR.send(this.resourceName);
            }
        },
        received: function (data) {
            if (data.Type == 'Message') {
                this.subject.onNext(data.Message);
            } else if (data.Type == 'Completed') {
                this.subject.onCompleted();
            } else if (data.Type == 'Error') {
                this.subject.onError(data.ErrorMessage);
            }
        },
        asObservable: function () {
            return this.subject.asObservable();
        }
    };

    $.signalR.prototype.resources = new Array();

    $.signalR.prototype.registerResource = function (resourceName, initialFilter) {
        if (this.resources[resourceName] != null) {
            if (initialFilter != null) {
                this.updateResourceFilter(resourceName, initialFilter);
            }
            return this.resources[resourceName];
        }
        var newResource = new Resource(this, resourceName, initialFilter);
        this.resources[resourceName] = newResource;

        this.stateChanged(function (change) {
            if (change.newState === $.signalR.connectionState.reconnecting || change.newState === $.signalR.connectionState.connected) {
                console.log('Connectied');
                for (resourceName in this.resources) {
                    this.resources[resourceName].onConnected();
                }
            }
        });

        return newResource;
    };

    $.signalR.prototype.updateResourceFilter = function (resourceName, filter) {
        if (this.resources[resourceName] != null) {
            this.resources[resourceName].updateFilter(filter);
        } else {
            this.registerResource(resourceName, filter);
        }
    };

    $.signalR.prototype.initializeResources = function () {
        var resourceName;

        for (resourceName in this.resources) {
            this.resources[resourceName].onConnected();
        }

        this.received(function (data) {
            if (data.Resource != null) {
                var resource = this.resources[data.Resource];
                if (resource != null) {
                    resource.received(data);
                }
            }
        });
    };
})();