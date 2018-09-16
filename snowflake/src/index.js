//@flow
import React from 'react';
import { render } from 'react-dom';
import Chart from './Chart';
import getData from './getData'

class ChartComponent extends React.Component<{}, {data1: Array<DataRow>, data2: Array<DataRow>}> {
	componentDidMount() {
		getData('backtest_SnowflakeBitMEXMeanReversionLimitAlgorithm_20180913_20180913.json').then(data1 => this.setState({data1}))
		getData('backtest_SnowflakeBitMEXMeanReversionMarketAlgorithm_20180913_20180913.json').then(data2 => this.setState({data2}))
	}
	render() {
		if (!this.state || !this.state.data1 || !this.state.data2) return <div>Loading...</div>
		return (<div>
							<Chart type={"hybrid"} data={this.state.data1} />
							<Chart type={"hybrid"} data={this.state.data2} />
					 </div>)
	}
}

//$FlowFixMe
render(<ChartComponent />, document.getElementById("root"));
