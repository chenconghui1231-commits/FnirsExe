function inspect_snirf_to_file(filepath)
    % 将SNIRF分析结果保存到文件
    output_file = 'snirf_analysis_result.txt';
    
    % 打开文件用于写入
    fid = fopen(output_file, 'w');
    if fid == -1
        error('无法创建输出文件');
    end
    
    % 重定向输出到文件
    fprintf(fid, '=== SNIRF文件结构分析 ===\n');
    fprintf(fid, '文件: %s\n', filepath);
    fprintf(fid, '分析时间: %s\n', datestr(now));
    
    % 获取文件信息
    file_info = h5info(filepath);
    fprintf(fid, '文件大小: %.2f MB\n', get_file_size(filepath));
    
    % 显示主要数据集
    fprintf(fid, '\n=== 主要数据集 ===\n');
    list_datasets_to_file(fid, file_info, '');
    
    % 读取关键数据
    fprintf(fid, '\n=== 关键数据内容 ===\n');
    read_key_data_to_file(fid, filepath);
    
    % 关闭文件
    fclose(fid);
    
    fprintf('分析完成！结果已保存到: %s\n', output_file);
    fprintf('用记事本打开查看完整结果。\n');
end

function list_datasets_to_file(fid, info, current_path)
    for i = 1:length(info.Groups)
        group = info.Groups(i);
        full_path = [current_path '/' group.Name];
        fprintf(fid, '组: %s\n', full_path);
        list_datasets_to_file(fid, group, full_path);
    end
    
    for i = 1:length(info.Datasets)
        dataset = info.Datasets(i);
        full_path = [current_path '/' dataset.Name];
        fprintf(fid, '数据集: %s (维度: %s)\n', full_path, mat2str(dataset.Dataspace.Size));
    end
end

function read_key_data_to_file(fid, filepath)
    try
        % 读取波长信息
        if h5exists(filepath, '/nirs/probe/wavelengths')
            wavelengths = h5read(filepath, '/nirs/probe/wavelengths');
            fprintf(fid, '波长: %s nm\n', mat2str(wavelengths'));
        end
        
        % 读取数据时间序列
        if h5exists(filepath, '/nirs/data1/dataTimeSeries')
            data_info = h5info(filepath, '/nirs/data1/dataTimeSeries');
            data_size = data_info.Dataspace.Size;
            fprintf(fid, '数据时间序列: %d 时间点 × %d 数据列\n', data_size(1), data_size(2));
        end
        
        % 读取测量列表信息
        fprintf(fid, '\n=== 测量列表信息 ===\n');
        read_measurement_lists_to_file(fid, filepath);
        
    catch ME
        fprintf(fid, '读取数据时出错: %s\n', ME.message);
    end
end

function read_measurement_lists_to_file(fid, filepath)
    ml_count = 0;
    i = 1;
    
    while true
        ml_path = sprintf('/nirs/data1/measurementList%d', i);
        if h5exists(filepath, ml_path)
            ml_count = ml_count + 1;
            
            try
                source_idx = h5read(filepath, [ml_path '/sourceIndex']);
                detector_idx = h5read(filepath, [ml_path '/detectorIndex']);
                wavelength_idx = h5read(filepath, [ml_path '/wavelengthIndex']);
                
                fprintf(fid, '测量列表 %d: 源=%d, 探测器=%d, 波长索引=%d', ...
                    i, source_idx, detector_idx, wavelength_idx);
                
                if h5exists(filepath, [ml_path '/dataTypeLabel'])
                    data_type = h5read(filepath, [ml_path '/dataTypeLabel']);
                    fprintf(fid, ' -> 数据类型: %s\n', data_type);
                else
                    fprintf(fid, '\n');
                end
                
            catch
                fprintf(fid, '测量列表 %d: 读取失败\n', i);
            end
            
            i = i + 1;
        else
            break;
        end
    end
    
    fprintf(fid, '\n总共找到 %d 个测量列表条目\n', ml_count);
end

function size_mb = get_file_size(filepath)
    info = dir(filepath);
    size_mb = info.bytes / (1024 * 1024);
end

function exists = h5exists(filepath, path)
    try
        info = h5info(filepath, path);
        exists = true;
    catch
        exists = false;
    end
end