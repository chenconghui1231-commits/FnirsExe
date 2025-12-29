function diagnose_snirf_file()
    file_path = 'E:\source\FnirsExe\AD\test.snirf';
    fprintf('诊断SNIRF文件: %s\n', file_path);
    fprintf('==================================================\n');
    
    % 检查文件是否存在
    if ~exist(file_path, 'file')
        fprintf('? 文件不存在: %s\n', file_path);
        return;
    end
    
    try
        % 检查波长信息
        fprintf('1. 检查波长信息...\n');
        try
            wavelengths = h5read(file_path, '/nirs/probe/wavelengths');
            fprintf('   ? 声明的波长: %s\n', mat2str(wavelengths'));
            num_wavelengths = length(wavelengths);
            fprintf('   波长数量: %d\n', num_wavelengths);
        catch
            fprintf('   ? 缺少 /nirs/probe/wavelengths\n');
            return;
        end
        
        % 检查数据矩阵
        fprintf('\n2. 检查数据矩阵...\n');
        try
            data_info = h5info(file_path, '/nirs/data1/dataTimeSeries');
            data_size = data_info.Dataspace.Size;
            fprintf('   ? dataTimeSeries 形状: %d × %d\n', data_size(1), data_size(2));
            fprintf('   时间点数: %d, 数据列数: %d\n', data_size(1), data_size(2));
        catch
            fprintf('   ? 缺少 /nirs/data1/dataTimeSeries\n');
        end
        
        % 检查measurementList
        fprintf('\n3. 检查measurementList...\n');
        ml_count = 0;
        wavelength_indices = [];
        
        i = 1;
        while true
            ml_path = sprintf('/nirs/data1/measurementList%d', i);
            try
                % 检查measurementList是否存在
                h5info(file_path, ml_path);
                ml_count = ml_count + 1;
                
                % 读取wavelengthIndex
                try
                    wl_idx = h5read(file_path, [ml_path '/wavelengthIndex']);
                    wavelength_indices(end+1) = wl_idx;
                    
                    % 读取其他信息
                    src_idx = h5read(file_path, [ml_path '/sourceIndex']);
                    det_idx = h5read(file_path, [ml_path '/detectorIndex']);
                    
                    fprintf('   %s: 源=%d, 探测器=%d, 波长索引=%d', ml_path, src_idx, det_idx, wl_idx);
                    if wl_idx >= 0 && wl_idx < num_wavelengths
                        fprintf(' (波长=%.1fnm)', wavelengths(wl_idx+1));
                    end
                    fprintf('\n');
                    
                catch
                    fprintf('   ??  %s: 缺少必要的索引字段\n', ml_path);
                end
                
                i = i + 1;
            catch
                break;
            end
        end
        
        fprintf('\n4. 分析结果:\n');
        fprintf('   找到 %d 个measurementList条目\n', ml_count);
        if ~isempty(wavelength_indices)
            fprintf('   使用的波长索引: %s\n', mat2str(unique(wavelength_indices)));
            
            % 检查缺失的波长
            expected_indices = 0:(num_wavelengths-1);
            missing_indices = setdiff(expected_indices, unique(wavelength_indices));
            
            if ~isempty(missing_indices)
                fprintf('   ? 问题确认: 缺少波长索引 %s\n', mat2str(missing_indices));
                fprintf('   缺失的波长: ');
                for i = 1:length(missing_indices)
                    fprintf('%.1fnm ', wavelengths(missing_indices(i)+1));
                end
                fprintf('\n');
            end
        end
        
    catch ME
        fprintf('? 错误: %s\n', ME.message);
    end
end