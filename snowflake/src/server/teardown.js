import cp from 'child_process'

try {
  cp.execSync('sudo rm -r ../data/crypto/gdax/tick/xbtusd')
  cp.execSync('sudo rm -r ../data/crypto/gdax/second/xbtusd')
  cp.execSync('sudo rm -r ../data/crypto/gdax/minute/xbtusd')
  cp.execSync('sudo rm -r ../data/crypto/gdax/hour/xbtusd')
  cp.execSync('sudo rm -r ../data/crypto/gdax/daily/xbtusd')
}
catch (e) {}
