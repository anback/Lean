//@flow
import React from 'react';
import { render } from 'react-dom';
import Chart from './Chart';
import getData from './getData'

class ChartComponent extends React.Component<{}, {data: Array<DataRow>}> {
	componentDidMount() {
		getData().then(data => this.setState({data}))
	}
	render() {
		if (this.state == null) {
			return <div>Loading...</div>
		}
		return <Chart type={"hybrid"} data={this.state.data} />
	}
}

//$FlowFixMe
render(<ChartComponent />, document.getElementById("root"));
