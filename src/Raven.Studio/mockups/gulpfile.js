var gulp = require('gulp'),
    util = require('gulp-util'),
    plumber = require('gulp-plumber'),
    rename = require('gulp-rename'),
    less = require('gulp-less'),
    gulpHandlebars = require('gulp-compile-handlebars'),
    handlebars = require('handlebars'),
    browserSync = require('browser-sync').create();

var TEMPLATE_OPTIONS = {
    batch : ['./src/partials']
};

var TEMPLATE_DATA = {};

gulp.task('partials', function () {
    return gulp.src('src/*.html')
    .pipe(addErrorHandling(gulpHandlebars(TEMPLATE_DATA, TEMPLATE_OPTIONS)))
    .pipe(gulp.dest('dist'));
});

gulp.task('less', function () {
    return gulp.src([
        '../wwwroot/Content/css/bootstrap/bootstrap.less',
        '../wwwroot/Content/css/styles.less'
    ], { base: '../wwwroot/Content' })
        .pipe(addErrorHandling(less()))
        .pipe(rename({
            extname: ".css"
        }))
        .pipe(gulp.dest('dist/content'));
});

gulp.task('copy-fonts', function () {
    return gulp.src([ '../wwwroot/Content/css/fonts/**'], { base: '../wwwroot/Content/' })
        .pipe(gulp.dest('dist/content'));
});

gulp.task('copy-js', function () {
    return gulp.src([ 'src/js/**/*.js' ])
        .pipe(gulp.dest('dist/js'));
});

gulp.task('copy-img', function () {
    return gulp.src([ '../wwwroot/Content/img/**/*' ])
        .pipe(gulp.dest('dist/content/img'));
});

gulp.task('copy-ace', function () {
    return gulp.src([ '../wwwroot/Content/ace/**/*' ], { base: '../wwwroot/Content' })
        .pipe(gulp.dest('dist/content'))
});

gulp.task('build', ['partials', 'less', 'copy-fonts', 'copy-img', 'copy-js', 'copy-ace']);

gulp.task('serve', ['build'], function() {

    browserSync.init({
        server: {
            baseDir: [ "dist" ]
        }
    });

    gulp.watch("../wwwroot/Content/css/**/*.less", [ 'less', browserSync.reload ]);
    gulp.watch("src/**/*.html", ['partials', browserSync.reload]);
    gulp.watch("src/js/**/*.js", ['copy-js', browserSync.reload]);
});

gulp.task('default', ['serve']);

function addErrorHandling(stream) {
    return stream.on('error', function (err) {
        util.log(err);
        this.emit('end');
    });
};
