function [HbO, HbR, HbT, channels] = calculate_Hb_concentration(snirf_file)
    % 计算血红蛋白浓度
    
    % 读取数据
    data = h5read(snirf_file, '/nirs/data1/dataTimeSeries');
    time = h5read(snirf_file, '/nirs/data1/time');
    
    % 消光系数 (756nm, 852nm)
    epsilon_HbO = [0.584, 1.484];
    epsilon_HbR = [1.048, 0.787];
    
    % 参数
    DPF = 5.0;
    L = 30;  % 需要根据实际探头布局调整
    
    % 分离波长数据
    wl1_data = data(1:22, :);
    wl2_data = data(23:44, :);
    
    % 计算光密度
    OD_wl1 = -log(wl1_data ./ mean(wl1_data, 2));
    OD_wl2 = -log(wl2_data ./ mean(wl2_data, 2));
    
    % 系数矩阵
    E = [epsilon_HbO(1), epsilon_HbR(1); 
         epsilon_HbO(2), epsilon_HbR(2)] * DPF * L;
    inv_E = inv(E);
    
    % 初始化输出
    num_channels = 22;
    HbO = zeros(num_channels, length(time));
    HbR = zeros(num_channels, length(time));
    
    % 为每个通道计算
    for i = 1:num_channels
        OD_756 = OD_wl1(i, :);
        OD_852 = OD_wl2(i, :);
        
        conc_change = inv_E * [OD_756; OD_852];
        
        HbO(i, :) = conc_change(1, :);
        HbR(i, :) = conc_change(2, :);
    end
    
    HbT = HbO + HbR;
    
    % 获取通道信息
    channels = get_channel_info(snirf_file);
end

function channels = get_channel_info(snirf_file)
    % 获取通道的源-探测器配对信息
    channels = struct();
    for i = 1:22
        ml_path = sprintf('/nirs/data1/measurementList%d/', i);
        try
            channels(i).sourceIndex = h5read(snirf_file, [ml_path 'sourceIndex']);
            channels(i).detectorIndex = h5read(snirf_file, [ml_path 'detectorIndex']);
            channels(i).wavelengthIndex = h5read(snirf_file, [ml_path 'wavelengthIndex']);
        catch
            channels(i).sourceIndex = NaN;
            channels(i).detectorIndex = NaN;
        end
    end
end