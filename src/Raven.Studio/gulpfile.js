﻿/// <binding />

require('./gulp/shim');

const gulp = require('gulp');
const exec = require('child_process').exec;
const parseHandlers = require('./gulp/parseHandlers');
const parseConfiguration = require('./gulp/parseConfiguration');
const gutil = require('gulp-util');
const fsUtils = require('./gulp/fsUtils');

const PATHS = require('./gulp/paths');

function z_parse_handlers() {
    return gulp.src(PATHS.handlersToParse)
        .pipe(parseHandlers('endpoints.ts'))
        .pipe(gulp.dest(PATHS.constantsTargetDir));
}
    
function z_parse_configuration() {
    return gulp.src(PATHS.configurationFilesToParse)
        .pipe(parseConfiguration('configuration.ts'))
        .pipe(gulp.dest(PATHS.constantsTargetDir));
}

function z_generate_typings(cb) {
    const possibleTypingsGenPaths = [
        '../../tools/TypingsGenerator/bin/Debug/net5.0/TypingsGenerator.dll',
        '../../tools/TypingsGenerator/bin/Release/net5.0/TypingsGenerator.dll' ];

    const dllPath = fsUtils.getLastRecentlyModifiedFile(possibleTypingsGenPaths);
    if (!dllPath) {
        cb(new Error('TypingsGenerator.dll not found neither for Release nor Debug directory.'));
        return;
    }

    gutil.log(`Running: dotnet ${dllPath}`);
    return exec('dotnet ' + dllPath, function (err, stdout, stderr) {
        if (err) {
            gutil.log(stdout);
            gutil.log(stderr);
        }

        cb(err);
    });
}

exports.prepare = gulp.parallel(z_parse_handlers, z_parse_configuration, z_generate_typings);
