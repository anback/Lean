// @flow
'use strict'
import express from 'express'
import morgan from 'morgan'

var path = require('path')

const app = express()


app.use(function(req, res, next) {
  res.header("Access-Control-Allow-Origin", "*");
  res.header("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
  next();
});

// Setup logger
app.use(morgan(':remote-addr - :remote-user [:date[clf]] ":method :url HTTP/:http-version" :status :res[content-length] :response-time ms'))

// Serve static assets
app.use(express.static(path.resolve(__dirname, '../../public')))
app.use(express.static(path.resolve(__dirname, '../../build')))
app.use(express.static(path.resolve(__dirname, '../../../Data/crypto/gdax/')))

const PORT = process.env.PORT || 9000

app.listen(PORT, () => console.log(`App listening on port ${PORT}!`))

export default app
