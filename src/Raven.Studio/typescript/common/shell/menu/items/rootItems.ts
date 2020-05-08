﻿import leafMenuItem = require("common/shell/menu/leafMenuItem");
import appUrl = require("common/appUrl");
import { about } from "../../../../viewmodels/shell/about";

function aboutItem() {
    return new leafMenuItem({
        route: 'about',
        moduleId: about,
        title: 'About',
        tooltip: "About",
        nav: true,
        css: 'icon-info',
        dynamicHash: appUrl.forAbout
    });
}

function serverDashboard() {
    return new leafMenuItem({
        route: ["", "dashboard"],
        moduleId: require('viewmodels/resources/serverDashboard'),
        title: 'Server Dashboard',
        tooltip: "Server Dashboard",
        nav: true,
        css: 'icon-dashboard',
        dynamicHash: appUrl.forServerDashboard
    });
}

export = {
    about: aboutItem,
    dashboard: serverDashboard
};
