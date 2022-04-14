const webpackConfigFunc = require('../webpack.config');
const path = require("path");
const webpackConfig = webpackConfigFunc(null, {
    mode: "development",
    watch: false
});

module.exports = {
  core: {
    builder: "webpack5"
  },
    babel: async (options) => {
      options.plugins.push(require.resolve("babel-plugin-replace-ts-export-assignment2"));
      return {
          ...options,
          sourceType: "unambiguous"
      }
    },
  "stories": [
    "../typescript/**/*.stories.tsx"
  ],
  "addons": [
    "@storybook/addon-links",
    "@storybook/addon-essentials",
    "@storybook/addon-interactions"
  ],
  "framework": "@storybook/react",
    webpackFinal: async config => {
        config.resolve.alias = { ...config.resolve.alias, ...webpackConfig.resolve.alias };

        config.plugins.unshift(webpackConfig.plugins.find(x => x.constructor.name === "ProvidePlugin"));
        
        //console.log(webpackConfig.module.rules.map(x => x.use));
        
        const incomingRules = webpackConfig.module.rules
            .filter(x => x.use && x.use.indexOf && x.use.indexOf("imports-loader") === 0);
        
        const assetsRules = webpackConfig.module.rules.filter(x => x.type && x.type.startsWith("asset"));
        config.module.rules = [
            {
                test: /\.html$/,
                use: {
                    loader: 'html-loader',
                    options: {
                        minimize: {
                            removeComments: false
                        }
                    }
                }
            },
            ...assetsRules, 
            ...config.module.rules, 
            webpackConfig.module.rules[1] // less rule
        ];
        
        config.plugins.push(webpackConfig.plugins[0]);
        
        config.module.rules.push(incomingRules[0]);
        
        return config;
    }
}
