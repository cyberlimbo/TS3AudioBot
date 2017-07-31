"use strict";
var ApiAuth = (function () {
    function ApiAuth(UserUid, Token) {
        this.UserUid = UserUid;
        this.Token = Token;
    }
    ApiAuth.prototype.hasValidNonce = function () {
        return this.CachedNonce !== undefined;
    };
    ApiAuth.prototype.generateResponse = function (url, realm) {
        if (this.ha1 === undefined || this.CachedRealm !== realm) {
            this.CachedRealm = realm;
            this.ha1 = md5(this.UserUid + ":" + realm + ":" + this.Token);
        }
        var ha2 = md5("GET" + ":" + url);
        return md5(this.ha1 + ":" + this.CachedRealm + ":" + ha2);
    };
    return ApiAuth;
}());
var AuthStatus;
(function (AuthStatus) {
    AuthStatus[AuthStatus["None"] = 0] = "None";
    AuthStatus[AuthStatus["FirstTry"] = 1] = "FirstTry";
    AuthStatus[AuthStatus["Failed"] = 2] = "Failed";
})(AuthStatus || (AuthStatus = {}));
var Get = (function () {
    function Get() {
    }
    Get.site = function (site, callback) {
        var xhr = new XMLHttpRequest();
        xhr.open("GET", site, true);
        if (callback !== undefined) {
            xhr.onload = function (ev) { return callback(xhr.responseText); };
            xhr.onerror = function (ev) { return callback(undefined); };
        }
        xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
        xhr.send();
    };
    Get.api = function (site, callback, login, status) {
        if (status === void 0) { status = AuthStatus.None; }
        if (login === undefined)
            throw new Error("Anonymous api call not supported yet");
        if (!login.hasValidNonce) {
            Get.getNonce(login, function (ok) {
                if (ok)
                    Get.api(site, callback, login, AuthStatus.FirstTry);
                else if (callback !== undefined)
                    callback(undefined);
            });
        }
    };
    Get.getNonce = function (login, callback) {
        var initReq = new XMLHttpRequest();
        initReq.open("GET", "/api/", true);
        initReq.setRequestHeader("Authorization", "Digest username=\"" + login.UserUid + "\"");
        initReq.onload = function (ev) {
            var ret = Get.extractNonce(initReq);
            login.CachedNonce = ret.nonce;
            login.CachedRealm = ret.realm;
            if (callback !== undefined)
                callback(true);
        };
        initReq.onerror = function (ev) { return callback(false); };
        initReq.send();
    };
    Get.extractNonce = function (request) {
        var digResponse = request.getResponseHeader("WWW-Authenticate");
        if (digResponse === null)
            throw new Error("No auth extracted");
        var digData = digResponse.match(/(realm|nonce)=\"\w+\"*/g);
        if (digData === null)
            throw new Error("No auth extracted");
        var realm;
        var nonce;
        for (var _i = 0, digData_1 = digData; _i < digData_1.length; _i++) {
            var param = digData_1[_i];
            var split = param.split(/=/);
            if (split[0] === "nonce")
                nonce = split[1];
            else if (param === "realm=")
                realm = split[1];
        }
        if (realm === undefined || nonce === undefined)
            throw new Error("Invalid auth data");
        return { nonce: nonce, realm: realm };
    };
    return Get;
}());
var Main = (function () {
    function Main() {
    }
    Main.init = function () {
        Main.contentDiv = document.getElementById("content");
        Main.initEvents();
        var currentSite = window.location.href;
        var query = Util.parseQuery(currentSite.substr(currentSite.indexOf("?") + 1));
        var page = query.page;
        if (page !== undefined) {
            Get.site("/" + page, Main.setContent);
        }
    };
    Main.initEvents = function () {
        var list = document.querySelectorAll("nav a");
        var _loop_1 = function (link) {
            var query = Util.parseQuery(link.href.substr(link.href.indexOf("?") + 1));
            var page = query.page;
            link.onclick = function (ev) {
                ev.preventDefault();
                Get.site(page, Main.setContent);
            };
        };
        for (var _i = 0, list_1 = list; _i < list_1.length; _i++) {
            var link = list_1[_i];
            _loop_1(link);
        }
    };
    Main.setContent = function (content) {
        Main.contentDiv.innerHTML = content;
    };
    return Main;
}());
window.onload = Main.init;
var Util = (function () {
    function Util() {
    }
    Util.parseQuery = function (query) {
        var search = /([^&=]+)=?([^&]*)/g;
        var decode = function (s) { return decodeURIComponent(s.replace(/\+/g, " ")); };
        var urlParams = {};
        var match = null;
        do {
            match = search.exec(query);
            if (match === null)
                break;
            urlParams[decode(match[1])] = decode(match[2]);
        } while (match !== null);
        return urlParams;
    };
    Util.value_to_logarithmic = function (val) {
        if (val < 0)
            val = 0;
        else if (val > Util.slmax)
            val = Util.slmax;
        return (1.0 / Util.log10(10 - val) - 1) * (Util.scale / (1.0 / Util.log10(10 - Util.slmax) - 1));
    };
    Util.logarithmic_to_value = function (val) {
        if (val < 0)
            val = 0;
        else if (val > Util.scale)
            val = Util.scale;
        return 10 - Math.pow(10, 1.0 / (val / (Util.scale / (1.0 / Util.log10(10 - Util.slmax) - 1)) + 1));
    };
    Util.log10 = function (val) {
        if (Math.log10 !== undefined)
            return Math.log10(val);
        else
            return Math.log(val) / Math.LN10;
    };
    return Util;
}());
Util.slmax = 7.0;
Util.scale = 100.0;
//# sourceMappingURL=script.js.map