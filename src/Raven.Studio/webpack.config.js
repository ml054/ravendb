const path = require("path");
const webpack = require('webpack');
const CircularDependencyPlugin = require("circular-dependency-plugin");

//TODO: use env to detect production build

module.exports = (env, args) => {
    return {
        entry: {
            main: "./typescript/main.ts",
        },
        devtool: 'eval-source-map',
        output: {
            path: __dirname + '/wwwroot/lib',
            filename: 'app.js'
        },
        plugins: [
            new CircularDependencyPlugin({
                // exclude detection of files based on a RegExp
                exclude: /node_modules/,
                failOnError: true,
                allowAsyncCycles: false,
                // set the current working directory for displaying module paths
                cwd: process.cwd(),
            }),
            new webpack.ProvidePlugin({
                ko: "knockout"
            })
        ],
        module: {
            rules: [
                {
                    test: /\.ts$/,
                    use: 'ts-loader'
                },
                {
                    test: /\.html$/,
                    use: {
                        loader: 'raw-loader',
                        options: {
                            esModule: false,
                        },
                    }
                },
                {
                    test: /\.(png|jpg|jpeg|gif|svg)$/,
                    use: [{
                        loader: 'url-loader',
                        options: {
                            name: 'img/[name].[hash:8].[ext]',
                            limit: 8192,
                            esModule: false
                        }
                    }]
                }
            ]
        },
        resolve: {
            modules: [path.resolve(__dirname, "../node_modules"), "node_modules"],
            extensions: ['.js', '.ts', '.tsx'],
            alias: {
                durandal: path.resolve(__dirname, 'wwwroot/lib/Durandal/js'),
                plugins: path.resolve(__dirname, 'wwwroot/lib/Durandal/js/plugins'),
                toastr: path.resolve(__dirname, 'wwwroot/lib/toastr/toastr'),
                moment: path.resolve(__dirname, 'wwwroot/lib/moment/moment'),
                jquery: path.resolve(__dirname, 'wwwroot/lib/jquery/dist/jquery'),
                common: path.resolve(__dirname, 'typescript/common'),
                models: path.resolve(__dirname, 'typescript/models'),
                durandalPlugins: path.resolve(__dirname, 'typescript/durandalPlugins'),
                commands: path.resolve(__dirname, 'typescript/commands'),
                viewmodels: path.resolve(__dirname, 'typescript/viewmodels'),
                widgets: path.resolve(__dirname, 'typescript/widgets'),
                overrides: path.resolve(__dirname, 'typescript/overrides'),
                endpoints: path.resolve(__dirname, 'typescript/endpoints'),
                views: path.resolve(__dirname, 'wwwroot/App/views'),
                Content: path.resolve(__dirname, 'wwwroot/Content/'),
                d3: path.resolve(__dirname, 'wwwroot/Content/custom_d3'),
                configuration: path.resolve(__dirname, 'typescript/configuration'),
                diff: path.resolve(__dirname, 'wwwroot/lib/diff'),
                cola: path.resolve(__dirname, "wwwroot/lib/webcola/WebCola/cola.min")
            }
        }
    };
};
