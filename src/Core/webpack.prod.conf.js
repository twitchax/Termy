const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const CleanWebpackPlugin = require('clean-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const WebpackShellPlugin = require('webpack-shell-plugin');
const webpack = require('webpack');

module.exports = {
  entry: {
    app: './wwwroot/index'
  },
  output: {
    filename: '[name].[chunkhash].js',
    path: path.resolve(__dirname, 'wwwroot/dist')
  },
  module: {
    rules: [
      {
        test: /\.(html)$/,
        use: {
          loader: 'html-loader'
        }
      },
      {
        test: /\.ts?$/,
        use: 'ts-loader',
        exclude: /node_modules/,
      }
    ]
  },
  resolve: {
    extensions: ['.ts', '.js']
  },
  plugins: [
    new CleanWebpackPlugin(['bin'], { root: path.resolve(__dirname) }),
    new CleanWebpackPlugin(['wwwroot/dist'], { root: path.resolve(__dirname) }),
    new webpack.NormalModuleReplacementPlugin(
      /environments\/environment\.ts/,
      'environment.prod.ts'
    ),
    new HtmlWebpackPlugin({
      template: './wwwroot/index.html'
    }),
    new CopyWebpackPlugin([
      {
        from: path.resolve(__dirname, './wwwroot/static'),
        to: 'static',
        ignore: ['.*']
      },
      {
        from: path.join(
          path.resolve(__dirname, './node_modules/@webcomponents/webcomponentsjs/'),
          '*.js'
        ),
        to: './webcomponentjs',
        flatten: true
      }
    ]),
    new webpack.IgnorePlugin(/vertx/),
    new WebpackShellPlugin({
      onBuildEnd: ['dotnet publish -c Release'] 
    })
  ]
};